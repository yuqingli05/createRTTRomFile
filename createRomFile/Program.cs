using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using File = System.IO.File;

namespace createRomFs
{
    class DataNode
    {
        public byte[] md5;
        public ulong address; // 数组地址
        public byte[] data;   // 存储的数据
        public DataNode()
        {
            md5 = new byte[0];
            data = new byte[0];
            address = 0;
        }
    };
    class RomFsNode
    {
        public uint type;
        public DataNode name; //名称地址
        public DataNode data; //存储的数据
        public RomFsNode()
        {
            type = 0;
            name = new DataNode();
            data = new DataNode();
        }
    };

    class Program
    {
        static ulong Alignment(ulong size)
        {
            return (size + 3) / 4 * 4;
        }


        static List<DataNode> use_data = new List<DataNode>();
        static List<DataNode> idle_data = new List<DataNode>();

        static string GetLnkSourcePath(string lnkpath)
        {
            if (System.IO.File.Exists(lnkpath))
            {
                WshShell shell = new WshShell();
                IWshShortcut lnk = (IWshShortcut)shell.CreateShortcut(lnkpath);
                return lnk.TargetPath;
            }
            else
            {
                return "";
            }
        }
        static byte[] GetMd5(byte[] data)
        {
            return MD5.Create().ComputeHash(data);
        }

        static bool AddData(byte[] data, out DataNode node_data)
        {
            ulong address = 0;
            ulong dataLen = Alignment((ulong)data.Length);
            byte[] md5 = GetMd5(data);

            foreach (DataNode i in use_data)
            {
                if (i.md5.SequenceEqual(md5))
                {
                    node_data = i;
                    return true;
                }

            }

            DataNode node_temp = null;
            foreach (DataNode i in idle_data)
            {
                if ((node_temp == null && (ulong)i.data.Length >= dataLen) ||
                    (node_temp != null && (ulong)i.data.Length >= dataLen && i.data.Length < node_temp.data.Length))
                {
                    node_temp = i;
                }
            }
            if (node_temp == null)
            {
                node_data = null;
                return false;
            }

            address = node_temp.address;

            // 删除重新添加
            if ((ulong)node_temp.data.Length - dataLen <= 0)
            {
                idle_data.Remove(node_temp);
            }
            else
            {
                node_temp.address += dataLen;
                node_temp.data = new byte[(ulong)node_temp.data.Length - dataLen];
            }


            Debug.WriteLine("创建一个内存区域 address=" + node_temp.address.ToString("x"));

            node_data = new DataNode();
            node_data.address = address;
            node_data.data = data;
            node_data.md5 = md5;
            use_data.Add(node_data);

            return true;
        }
        static bool AddDataInAddress(byte[] data, out DataNode node_data, ulong Address)
        {
            byte[] md5 = GetMd5(data);
            ulong dataLen = Alignment((ulong)data.Length);

            DataNode node_temp = null;
            foreach (DataNode i in idle_data)
            {
                if (Address >= i.address && Address + dataLen <= i.address + (ulong)i.data.Length)
                {
                    node_temp = i;
                    break;
                }
            }
            if (node_temp == null)
            {
                node_data = null;
                return false;
            }

            // 删除重新添加
            idle_data.Remove(node_temp);
            if (Address + dataLen < node_temp.address + (ulong)node_temp.data.Length)
            {
                DataNode node = new DataNode();
                node.address = Address + dataLen;
                node.data = new byte[(node_temp.address + (ulong)node_temp.data.Length) - (Address + dataLen)];
                idle_data.Add(node);
            }
            if (Address > node_temp.address)
            {
                DataNode node = new DataNode();
                node.address = node_temp.address;
                node.data = new byte[Address - node_temp.address];
                idle_data.Add(node);
            }

            Debug.WriteLine("创建一个内存区域 address=" + node_temp.address.ToString("x"));

            node_data = new DataNode();
            node_data.address = Address;
            node_data.data = data;
            node_data.md5 = md5;
            use_data.Add(node_data);

            return true;
        }
        static bool mkFile(string path, out RomFsNode node)
        {
            node = new RomFsNode();

            if (!File.Exists(path))
            {
                return false;
            }

            int dataLen = (int)(new FileInfo(path).Length);
            byte[] data = new byte[dataLen];
            try
            {
                using (FileStream fopen = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fopen.Read(data, 0, dataLen) != dataLen)
                    {
                        return false;
                    }
                }
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return false;
            }

            Debug.WriteLine(path);

            if (AddData(Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\0"), out node.name))
            {
                if (AddData(data, out node.data))
                {
                    return true;
                }
            }
            return false;
        }
        static bool mkDir(string path, out RomFsNode node, int depth)
        {
            node = new RomFsNode();
            node.type = 1;

            if (!Directory.Exists(path) || depth > 16)
            {
                return false;
            }

            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo[] fils_infos = dir.GetFiles();
            DirectoryInfo[] dir_infos = dir.GetDirectories();

            List<byte> head = new List<byte>();

            // 变量文件
            foreach (FileInfo f in fils_infos)
            {
                bool islnk = f.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);

                if (islnk)
                {
                    //快捷方式
                    // 去掉后缀之后 不存在同名文件或者路径
                    string str_temp = Path.Combine(f.DirectoryName, Path.GetFileNameWithoutExtension(f.FullName));
                    if (!File.Exists(str_temp) && !Directory.Exists(str_temp))
                    {
                        bool mksuccess = false;
                        string filePath = GetLnkSourcePath(f.FullName);
                        RomFsNode node_temp = new RomFsNode();
                        if (File.Exists(filePath))
                            mksuccess = mkFile(filePath, out node_temp);
                        else if (Directory.Exists(filePath))
                            mksuccess = mkDir(filePath, out node_temp, depth + 1);

                        if (mksuccess)
                        {
                            // 连接文件要重新添加 名称
                            Debug.WriteLine(f.FullName);
                            DataNode data_name;
                            if (AddData(Encoding.UTF8.GetBytes(Path.GetFileNameWithoutExtension(f.FullName) + "\0"), out data_name))
                            {
                                //添加到目录数组
                                head.Add((byte)node_temp.type);
                                head.Add((byte)(node_temp.type >> 8));
                                head.Add((byte)(node_temp.type >> 16));
                                head.Add((byte)(node_temp.type >> 24));

                                head.Add((byte)data_name.address);
                                head.Add((byte)(data_name.address >> 8));
                                head.Add((byte)(data_name.address >> 16));
                                head.Add((byte)(data_name.address >> 24));

                                head.Add((byte)node_temp.data.address);
                                head.Add((byte)(node_temp.data.address >> 8));
                                head.Add((byte)(node_temp.data.address >> 16));
                                head.Add((byte)(node_temp.data.address >> 24));

                                uint datalen;
                                if (node_temp.type == 1)
                                    datalen = (uint)(node_temp.data.data.Length / 16);
                                else
                                    datalen = (uint)(node_temp.data.data.Length);
                                head.Add((byte)datalen);
                                head.Add((byte)(datalen >> 8));
                                head.Add((byte)(datalen >> 16));
                                head.Add((byte)(datalen >> 24));
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    RomFsNode node_temp;
                    if (mkFile(f.FullName, out node_temp))
                    {
                        //添加到目录数组
                        head.Add((byte)node_temp.type);
                        head.Add((byte)(node_temp.type >> 8));
                        head.Add((byte)(node_temp.type >> 16));
                        head.Add((byte)(node_temp.type >> 24));

                        head.Add((byte)node_temp.name.address);
                        head.Add((byte)(node_temp.name.address >> 8));
                        head.Add((byte)(node_temp.name.address >> 16));
                        head.Add((byte)(node_temp.name.address >> 24));

                        head.Add((byte)node_temp.data.address);
                        head.Add((byte)(node_temp.data.address >> 8));
                        head.Add((byte)(node_temp.data.address >> 16));
                        head.Add((byte)(node_temp.data.address >> 24));

                        uint datalen = (uint)(node_temp.data.data.Length);
                        head.Add((byte)datalen);
                        head.Add((byte)(datalen >> 8));
                        head.Add((byte)(datalen >> 16));
                        head.Add((byte)(datalen >> 24));
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // 遍历目录
            foreach (DirectoryInfo d in dir_infos)
            {
                RomFsNode node_temp;
                if (mkDir(d.FullName, out node_temp, depth + 1))
                {
                    //添加到目录数组
                    head.Add((byte)node_temp.type);
                    head.Add((byte)(node_temp.type >> 8));
                    head.Add((byte)(node_temp.type >> 16));
                    head.Add((byte)(node_temp.type >> 24));

                    head.Add((byte)node_temp.name.address);
                    head.Add((byte)(node_temp.name.address >> 8));
                    head.Add((byte)(node_temp.name.address >> 16));
                    head.Add((byte)(node_temp.name.address >> 24));

                    head.Add((byte)node_temp.data.address);
                    head.Add((byte)(node_temp.data.address >> 8));
                    head.Add((byte)(node_temp.data.address >> 16));
                    head.Add((byte)(node_temp.data.address >> 24));

                    uint datalen = (uint)(node_temp.data.data.Length / 16);
                    head.Add((byte)datalen);
                    head.Add((byte)(datalen >> 8));
                    head.Add((byte)(datalen >> 16));
                    head.Add((byte)(datalen >> 24));
                }
                else
                {
                    return false;
                }
            }

            Debug.WriteLine(path);
            // 添加当前目录列表
            if (AddData(Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\0"), out node.name))
            {
                if (AddData(head.ToArray(), out node.data))
                {
                    return true;
                }
            }
            return false;
        }
        static ulong mkRomFs(ulong Address, ulong len, string path, ulong entryAddress)
        {
            RomFsNode node_temp;
            DataNode StartNode = new DataNode();
            DataNode TotalNode = new DataNode();

            TotalNode.data = new byte[len];
            TotalNode.address = Address;

            use_data = new List<DataNode>();
            idle_data = new List<DataNode>();
            idle_data.Add(TotalNode);

            if (AddDataInAddress(new byte[16], out StartNode, entryAddress))
            {
                StartNode.md5 = new byte[0]; // 清空md5 防止对比成功 因为后面还会改变,现在只是站位
                if (mkDir(Path.GetFullPath(path), out node_temp, 0))
                {
                    StartNode.data[0] = (byte)node_temp.type;
                    StartNode.data[1] = (byte)(node_temp.type >> 8);
                    StartNode.data[2] = (byte)(node_temp.type >> 16);
                    StartNode.data[3] = (byte)(node_temp.type >> 24);

                    StartNode.data[4] = (byte)node_temp.name.address;
                    StartNode.data[5] = (byte)(node_temp.name.address >> 8);
                    StartNode.data[6] = (byte)(node_temp.name.address >> 16);
                    StartNode.data[7] = (byte)(node_temp.name.address >> 24);

                    StartNode.data[8] = (byte)node_temp.data.address;
                    StartNode.data[9] = (byte)(node_temp.data.address >> 8);
                    StartNode.data[10] = (byte)(node_temp.data.address >> 16);
                    StartNode.data[11] = (byte)(node_temp.data.address >> 24);

                    uint datalen = (uint)(node_temp.data.data.Length / 16);
                    StartNode.data[12] = (byte)datalen;
                    StartNode.data[13] = (byte)(datalen >> 8);
                    StartNode.data[14] = (byte)(datalen >> 16);
                    StartNode.data[15] = (byte)(datalen >> 24);

                    StartNode.md5 = GetMd5(StartNode.data);

                    ulong endAddress = Address;
                    foreach (DataNode i in use_data)
                    {
                        if (i.address + (ulong)i.data.Length > endAddress)
                            endAddress = i.address + (ulong)i.data.Length;
                    }
                    return endAddress - Address;
                }
            }
            return 0;
        }
        static byte[] mkRomFsBinary(ulong Address, ulong len)
        {
            byte[] binary = new byte[len];
            for (int i = 0; i < binary.Length; i++)
                binary[i] = 0xFF;

            foreach (DataNode i in use_data)
            {
                Array.Copy(i.data, 0, binary, (int)(i.address - Address), i.data.Length);
            }

            return binary;
        }
        static bool SaveBinary(string path, byte[] binary)
        {
            try
            {
                using (FileStream fopen = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fopen.Write(binary, 0, binary.Length);
                }
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return false;
            }
            return true;
        }

        static int Main(string[] args)
        {
            string InPath = "";
            string OutPath = "";

            ulong BinAddress = 0;         // 0
            ulong BinLen = 10 * 1024 * 1024; // 10M
            ulong EntryAddress = 0;       // 0

            if (args.Length < 3 || args.Length > 5)
            {
                Console.WriteLine("eg: createRomFs.exe c://path c://romfs.bin BinAddress");
                Console.WriteLine("eg: createRomFs.exe c://path c://romfs.bin BinAddress BinLen EntryAddress");
                return -1;
            }


            InPath = args[0];
            if (!Directory.Exists(InPath))
            {
                Console.WriteLine("路径[" + InPath + "]不存在");
                return -1;
            }
            OutPath = args[1];
            try
            {
                if (args[2].Substring(0, 2) == "0X" || args[2].Substring(0, 2) == "0x")
                    BinAddress = ulong.Parse(args[2].Substring(2, args[2].Length - 2), System.Globalization.NumberStyles.HexNumber);
                else
                    BinAddress = ulong.Parse(args[2]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("地址参数错误 " + args[2]);
                return -1;
            }

            if (args.Length >= 4)
            {
                try
                {
                    if (args[3].Substring(0, 2) == "0X" || args[3].Substring(0, 2) == "0x")
                        BinLen = ulong.Parse(args[3].Substring(2, args[3].Length - 2), System.Globalization.NumberStyles.HexNumber);
                    else
                        BinLen = ulong.Parse(args[3]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("长度参数错误 " + args[3]);
                    return -1;
                }
            }

            if (args.Length >= 5)
            {
                try
                {
                    if (args[4].Substring(0, 2) == "0X" || args[4].Substring(0, 2) == "0x")
                        EntryAddress = ulong.Parse(args[4].Substring(2, args[4].Length - 2), System.Globalization.NumberStyles.HexNumber);
                    else
                        EntryAddress = ulong.Parse(args[4]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("入口地址错误 " + args[4]);
                    return -1;
                }
            }
            else
            {
                EntryAddress = BinAddress;
            }

            if (EntryAddress < BinAddress || EntryAddress >= BinAddress + BinLen)
            {
                Console.WriteLine("入口地址超出固件范围");
                return -1;
            }


            ulong len = mkRomFs(BinAddress, BinLen, InPath, EntryAddress);
            byte[] binary = mkRomFsBinary(BinAddress, len);
            Debug.WriteLine("文件系统大小 " + len.ToString());
            if (binary.Length > 0 && !SaveBinary(OutPath, binary))
            {
                Console.WriteLine("保存文件失败,可能是权限不够");
                return -1;
            }
            Console.WriteLine("生成文件成功" + OutPath);
            return 0;
        }

    }
}




