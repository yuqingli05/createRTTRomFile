using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace createRomFs
{
    class Program
    {
        static ulong Alignment(ulong size)
        {
            return (size + 3) / 4 * 4;
        }
        struct RomNode
        {
            public uint type;
            public ulong nameAddress; //名称地址
            public byte[] name;       //存储名称
            public ulong dataAddress; //数组地址
            public byte[] data;       //存储的数据
        };
        struct LinkMap
        {
            public string source;
            public string link;
        };

        static List<LinkMap> ReadCsv(string path)
        {
            StreamReader sr;
            List<LinkMap> maps = new List<LinkMap>();
            try
            {
                using (sr = new StreamReader(path, Encoding.GetEncoding("UTF-8")))
                {
                    string str = "";
                    while ((str = sr.ReadLine()) != null)
                    {
                        string[] strs = str.Split(',');
                        if (strs.Length == 2)
                        {
                            LinkMap map = new LinkMap();
                            map.source = strs[0];
                            map.link = strs[1];
                            maps.Add(map);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return maps;
        }
        static long mkLink(ulong Address, string name, RomNode sourceNode, out RomNode node)
        {
            ulong startAddress = Address;

            node = new RomNode();
            node.dataAddress = sourceNode.dataAddress;
            node.data = sourceNode.data;
            node.type = sourceNode.type;

            node.name = Encoding.UTF8.GetBytes(name + "\0");
            node.nameAddress = Address;
            Address += Alignment((ulong)node.name.Length);

            return (long)(Address - startAddress);
        }
        static long mkFile(ulong Address, string path, out RomNode node)
        {
            ulong startAddress = Address;
            node = new RomNode();
            node.nameAddress = 0;
            node.dataAddress = 0;
            node.type = 0;

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

            node.type = 0;
            node.name = Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\0");
            node.nameAddress = Address;
            Address += Alignment((ulong)node.name.Length);
            node.data = data;
            node.dataAddress = Address;
            Address += Alignment((ulong)node.data.Length);

            return (long)(Address - startAddress);
        }
        static long mkDir(ulong Address, string path, out List<RomNode> nodes)
        {
            ulong startAddress = Address;
            nodes = new List<RomNode>();

            List<LinkMap> maps = ReadCsv(Path.Combine(path, "__link.csv"));
            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo[] fils_infos = dir.GetFiles();
            DirectoryInfo[] dir_infos = dir.GetDirectories();

            List<byte> head = new List<byte>();

            // 变量文件
            foreach (FileInfo f in fils_infos)
            {
                if (f.Name == "__link.csv")
                    continue; //跳过链接文件

                RomNode node_temp;
                long len = mkFile(Address, f.FullName, out node_temp);
                if (len > 0)
                {
                    Address += (ulong)len;
                    nodes.Add(node_temp);

                    //添加到目录数组
                    head.Add((byte)node_temp.type);
                    head.Add((byte)(node_temp.type >> 8));
                    head.Add((byte)(node_temp.type >> 16));
                    head.Add((byte)(node_temp.type >> 24));

                    head.Add((byte)node_temp.nameAddress);
                    head.Add((byte)(node_temp.nameAddress >> 8));
                    head.Add((byte)(node_temp.nameAddress >> 16));
                    head.Add((byte)(node_temp.nameAddress >> 24));

                    head.Add((byte)node_temp.dataAddress);
                    head.Add((byte)(node_temp.dataAddress >> 8));
                    head.Add((byte)(node_temp.dataAddress >> 16));
                    head.Add((byte)(node_temp.dataAddress >> 24));

                    uint datalen = (uint)(node_temp.data.Length);
                    head.Add((byte)datalen);
                    head.Add((byte)(datalen >> 8));
                    head.Add((byte)(datalen >> 16));
                    head.Add((byte)(datalen >> 24));
                }
            }

            // 遍历目录
            foreach (DirectoryInfo d in dir_infos)
            {
                List<RomNode> nodes_temp;
                long len = mkDir(Address, d.FullName, out nodes_temp);
                if (len > 0)
                {
                    Address += (ulong)len;
                    nodes.AddRange(nodes_temp);

                    RomNode node_temp = nodes_temp.Last();

                    //添加到目录数组
                    head.Add((byte)node_temp.type);
                    head.Add((byte)(node_temp.type >> 8));
                    head.Add((byte)(node_temp.type >> 16));
                    head.Add((byte)(node_temp.type >> 24));

                    head.Add((byte)node_temp.nameAddress);
                    head.Add((byte)(node_temp.nameAddress >> 8));
                    head.Add((byte)(node_temp.nameAddress >> 16));
                    head.Add((byte)(node_temp.nameAddress >> 24));

                    head.Add((byte)node_temp.dataAddress);
                    head.Add((byte)(node_temp.dataAddress >> 8));
                    head.Add((byte)(node_temp.dataAddress >> 16));
                    head.Add((byte)(node_temp.dataAddress >> 24));

                    uint datalen = (uint)(node_temp.data.Length / 16);
                    head.Add((byte)datalen);
                    head.Add((byte)(datalen >> 8));
                    head.Add((byte)(datalen >> 16));
                    head.Add((byte)(datalen >> 24));
                }
            }

            // 遍历映射
            foreach (LinkMap map in maps)
            {
                byte[] name = Encoding.UTF8.GetBytes(map.source + "\0");
                foreach (RomNode node in nodes)
                {
                    if (Enumerable.SequenceEqual(node.name, name))
                    {
                        RomNode node_temp;
                        long len = mkLink(Address, map.link, node, out node_temp);
                        if (len > 0)
                        {
                            Address += (ulong)len;
                            nodes.Add(node_temp);

                            //添加到目录数组
                            head.Add((byte)node_temp.type);
                            head.Add((byte)(node_temp.type >> 8));
                            head.Add((byte)(node_temp.type >> 16));
                            head.Add((byte)(node_temp.type >> 24));

                            head.Add((byte)node_temp.nameAddress);
                            head.Add((byte)(node_temp.nameAddress >> 8));
                            head.Add((byte)(node_temp.nameAddress >> 16));
                            head.Add((byte)(node_temp.nameAddress >> 24));

                            head.Add((byte)node_temp.dataAddress);
                            head.Add((byte)(node_temp.dataAddress >> 8));
                            head.Add((byte)(node_temp.dataAddress >> 16));
                            head.Add((byte)(node_temp.dataAddress >> 24));

                            uint datalen;
                            if (node_temp.type == 0)
                                datalen = (uint)(node_temp.data.Length);
                            else
                                datalen = (uint)(node_temp.data.Length / 16);

                            head.Add((byte)datalen);
                            head.Add((byte)(datalen >> 8));
                            head.Add((byte)(datalen >> 16));
                            head.Add((byte)(datalen >> 24));
                        }
                        break;
                    }
                }

            }

            // 添加当前目录列表
            {
                // 添加一个目录 node
                RomNode node = new RomNode();
                node.type = 0x01;
                node.name = Encoding.UTF8.GetBytes(dir.Name + "\0");
                node.nameAddress = Address;
                Address += Alignment((ulong)node.name.Length);
                node.data = head.ToArray();
                node.dataAddress = Address;
                Address += Alignment((ulong)node.data.Length);
                nodes.Add(node);
            }

            return (long)(Address - startAddress);
        }
        static long mkRomFs(ulong Address, string path, out List<RomNode> nodes)
        {
            long len = mkDir(Address + 16, path, out nodes);

            // 添加根路径
            {
                RomNode node_temp = nodes.Last();
                List<byte> head = new List<byte>();
                head.Add((byte)node_temp.type);
                head.Add((byte)(node_temp.type >> 8));
                head.Add((byte)(node_temp.type >> 16));
                head.Add((byte)(node_temp.type >> 24));

                head.Add((byte)node_temp.nameAddress);
                head.Add((byte)(node_temp.nameAddress >> 8));
                head.Add((byte)(node_temp.nameAddress >> 16));
                head.Add((byte)(node_temp.nameAddress >> 24));

                head.Add((byte)node_temp.dataAddress);
                head.Add((byte)(node_temp.dataAddress >> 8));
                head.Add((byte)(node_temp.dataAddress >> 16));
                head.Add((byte)(node_temp.dataAddress >> 24));

                uint datalen = (uint)(node_temp.data.Length / 16);
                head.Add((byte)datalen);
                head.Add((byte)(datalen >> 8));
                head.Add((byte)(datalen >> 16));
                head.Add((byte)(datalen >> 24));



                // 添加根路径
                RomNode node = new RomNode();
                node.type = 0x01;
                node.name = new byte[0]; //根路径是没有名称的 因为不需要上级
                node.nameAddress = 0;
                node.data = head.ToArray();
                node.dataAddress = Address;
                nodes.Add(node);
            }

            return len + 16;
        }
        static byte[] mkRomFsBinary(ulong Address, long len, List<RomNode> nodes)
        {
            byte[] binary = new byte[len];
            for (int i = 0; i < binary.Length; i++)
                binary[i] = 0xFF;

            foreach (RomNode node_temp in nodes)
            {
                int startIndex;
                // 拷贝名称
                if (node_temp.nameAddress != 0 && node_temp.name != null && node_temp.name.Length > 0)
                {
                    startIndex = (int)(node_temp.nameAddress - Address);
                    Array.Copy(node_temp.name, 0, binary, startIndex, node_temp.name.Length);
                }
                // 拷贝数据
                if (node_temp.dataAddress != 0 && node_temp.data != null && node_temp.data.Length > 0)
                {
                    startIndex = (int)(node_temp.dataAddress - Address);
                    Array.Copy(node_temp.data, 0, binary, startIndex, node_temp.data.Length);
                }
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

            string Path = args[0];
            if (!Directory.Exists(Path))
            {
                Console.WriteLine("路径[" + Path + "]不存在");
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

            List<RomNode> nodes;
            long len = mkRomFs(Address, Path, out nodes);
            byte[] binary = mkRomFsBinary(Address, len, nodes);

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



        
