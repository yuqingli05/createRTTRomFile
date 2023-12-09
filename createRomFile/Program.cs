using IWshRuntimeLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using File = System.IO.File;

namespace createRomFs
{
    class Program
    {
        static ulong Alignment(ulong size)
        {
            return (size + 3) / 4 * 4;
        }
        struct DataNode
        {
            public byte[] md5;
            public ulong address; //数组地址
            public byte[] data;       //存储的数据
        };
        struct RomFsNode
        {
            public uint type;
            public DataNode name; //名称地址
            public DataNode data; //存储的数据
        };

        static List<DataNode> all_data = new List<DataNode>();

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

        static long AddData(ulong Address, byte[] data, out DataNode node_data)
        {
            byte[] md5 = GetMd5(data);

            foreach (DataNode i in all_data)
            {
                if (i.md5.SequenceEqual(md5))
                {
                    node_data = i;
                    return 0;
                }
            }

            Debug.WriteLine("创建一个内存区域 address=" + Address.ToString("x"));

            node_data = new DataNode();
            node_data.address = Address;
            node_data.data = data;
            node_data.md5 = md5;
            all_data.Add(node_data);
            return data.Length;
        }
        static long mkFile(ulong Address, string path, out RomFsNode node)
        {
            ulong startAddress = Address;
            long len;
            node = new RomFsNode();
            node.type = 0;
            node.name.address = 0;
            node.name.data = new byte[0];
            node.data.address = 0;
            node.data.data = new byte[0];

            if (!File.Exists(path))
            {
                return -1;
            }

            int dataLen = (int)(new FileInfo(path).Length);
            byte[] data = new byte[dataLen];
            try
            {
                using (FileStream fopen = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    if (fopen.Read(data, 0, dataLen) != dataLen)
                    {
                        return -1;
                    }
                }
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return -1;
            }

            Debug.WriteLine(path);

            len = AddData(Address, Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\0"), out node.name);
            Address += Alignment((ulong)len);

            len = AddData(Address, data, out node.data);
            Address += Alignment((ulong)len);

            return (long)(Address - startAddress);
        }
        static long mkDir(ulong Address, string path, out RomFsNode node)
        {
            ulong startAddress = Address;
            long len;

            node = new RomFsNode();
            node.type = 1;
            node.name.address = 0;
            node.name.data = new byte[0];
            node.data.address = 0;
            node.data.data = new byte[0];

            if (!Directory.Exists(path))
            {
                return -1;
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
                        string filePath = GetLnkSourcePath(f.FullName);
                        RomFsNode node_temp = new RomFsNode();
                        if (File.Exists(filePath))
                            len = mkFile(Address, filePath, out node_temp);
                        else if (Directory.Exists(filePath))
                            len = mkDir(Address, filePath, out node_temp);
                        else
                            len = -1;


                        if (len >= 0)
                        {
                            Address += (ulong)len;

                            //添加到目录数组
                            head.Add((byte)node_temp.type);
                            head.Add((byte)(node_temp.type >> 8));
                            head.Add((byte)(node_temp.type >> 16));
                            head.Add((byte)(node_temp.type >> 24));

                            // 连接文件要重新添加 名称
                            Debug.WriteLine(f.FullName);
                            DataNode data_name;
                            len = AddData(Address, Encoding.UTF8.GetBytes(Path.GetFileNameWithoutExtension(f.FullName) + "\0"), out data_name); //去掉后缀
                            Address += Alignment((ulong)len);
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
                    }
                }
                else
                {
                    RomFsNode node_temp;
                    len = mkFile(Address, f.FullName, out node_temp);
                    if (len >= 0)
                    {
                        Address += (ulong)len;

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
                }
            }

            // 遍历目录
            foreach (DirectoryInfo d in dir_infos)
            {
                RomFsNode node_temp;
                len = mkDir(Address, d.FullName, out node_temp);
                if (len >= 0)
                {
                    Address += (ulong)len;

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
            }

            Debug.WriteLine(path);
            // 添加当前目录列表
            len = AddData(Address, Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\0"), out node.name);
            Address += Alignment((ulong)len);

            len = AddData(Address, head.ToArray(), out node.data);
            Address += Alignment((ulong)len);

            return (long)(Address - startAddress);
        }
        static long mkRomFs(ulong Address, string path)
        {
            RomFsNode node_temp;
            DataNode data;
            long len = mkDir(Address + 16, Path.GetFullPath(path), out node_temp);

            Debug.WriteLine("创建根目录");

            // 添加根路径
            List<byte> head = new List<byte>();
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

            len += AddData(Address, head.ToArray(), out data);

            return len;
        }
        static byte[] mkRomFsBinary(ulong Address, long len)
        {
            byte[] binary = new byte[len];
            for (int i = 0; i < binary.Length; i++)
                binary[i] = 0xFF;

            foreach (DataNode i in all_data)
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
            if (args.Length != 3)
            {
                Console.WriteLine("eg: createRomFs.exe c://path c://romfs.bin 0x00");
                return -1;
            }

            string path = args[0];
            if (!Directory.Exists(path))
            {
                Console.WriteLine("路径[" + path + "]不存在");
                return -1;
            }

            string OutPath = args[1];
            ulong Address;
            try
            {
                if (args[2].Substring(0, 2) == "0X" || args[2].Substring(0, 2) == "0x")
                    Address = ulong.Parse(args[2].Substring(2, args[2].Length - 2), System.Globalization.NumberStyles.HexNumber);
                else
                    Address = ulong.Parse(args[2]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("地址参数错误 " + args[2]);
                return -1;
            }

            long len = mkRomFs(Address, path);
            byte[] binary = mkRomFsBinary(Address, len);
            Debug.WriteLine("文件系统大小 " + len.ToString());

            if (!SaveBinary(OutPath, binary))
            {
                Console.WriteLine("保存文件失败,可能是权限不够");
                return -1;
            }

            Console.WriteLine("生成文件成功" + OutPath);
            return 0;
        }

    }
}




