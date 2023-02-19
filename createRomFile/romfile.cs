using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace createRomFs
{
    class romfs
    {
        struct fileNode
        {
            public string filename;
            public string source;
            public int type;
            public byte[] name;
            public byte[] data;
            public int dataLen;
            public uint nameAddress;
            public uint dataAddress;
        };

        List<fileNode> filelist = new List<fileNode>();
        uint dataAddress = 0;

        public romfs()
        {

        }
        ~romfs()
        {

        }

        public bool saveBin(string path, uint address)
        {
            List<Byte> data = new List<Byte>();
            List<Byte> head = new List<Byte>();

            head.Add((byte)filelist.Count);
            head.Add((byte)(filelist.Count >> 8));
            head.Add((byte)(filelist.Count >> 16));
            head.Add((byte)(filelist.Count >> 24));
            head.Add((byte)(~filelist.Count));
            head.Add((byte)((~filelist.Count) >> 8));
            head.Add((byte)((~filelist.Count) >> 16));
            head.Add((byte)((~filelist.Count) >> 24));

            foreach (fileNode f in filelist)
            {
                uint temp;
                data = data.Concat(f.name.ToList()).ToList();
                if (f.data != null && f.data.Length != 0 && f.dataLen != 0)
                    data = data.Concat(f.data.ToList()).ToList();

                head.Add((byte)f.type);
                head.Add((byte)(f.type >> 8));
                head.Add((byte)(f.type >> 16));
                head.Add((byte)(f.type >> 24));

                temp = address + f.nameAddress + (uint)filelist.Count * 16 + 8;
                head.Add((byte)temp);
                head.Add((byte)(temp >> 8));
                head.Add((byte)(temp >> 16));
                head.Add((byte)(temp >> 24));

                temp = address + f.dataAddress + (uint)filelist.Count * 16 + 8;
                head.Add((byte)temp);
                head.Add((byte)(temp >> 8));
                head.Add((byte)(temp >> 16));
                head.Add((byte)(temp >> 24));

                head.Add((byte)f.dataLen);
                head.Add((byte)(f.dataLen >> 8));
                head.Add((byte)(f.dataLen >> 16));
                head.Add((byte)(f.dataLen >> 24));
            }

            head = head.Concat(data).ToList();

            byte[] romf = head.ToArray();
            try
            {
                using (FileStream fopen = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    fopen.Write(romf, 0, romf.Length);
                }
            }
            catch (System.UnauthorizedAccessException ex)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 添加一个连接 重复文件不同名字 用于节省内存，原文件必须先添加
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool addLink(string source, string target)
        {
            foreach (fileNode f in filelist)
            {
                if (f.filename == source)
                {
                    fileNode node = new fileNode();

                    byte[] name = Encoding.UTF8.GetBytes(target + "\0");
                    node.name = new byte[(name.Length + 3) / 4 * 4];
                    name.CopyTo(node.name, 0);
                    node.data = null; //链接文件没有源数据
                    node.type = 0;
                    node.dataLen = f.dataLen;
                    node.source = f.filename;
                    node.filename = target;
                    node.nameAddress = dataAddress;
                    dataAddress += (uint)node.name.Length;
                    node.dataAddress = f.dataAddress;
                    filelist.Add(node);
                    return true;
                }
            }
            return false;
        }
        public bool addFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(path) + "\0");
            int dataLen = (int)(new FileInfo(path).Length);
            byte[] data = new byte[(dataLen + 3) / 4 * 4];
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

            fileNode node = new fileNode();
            node.name = new byte[(name.Length + 3) / 4 * 4];
            name.CopyTo(node.name, 0);
            node.data = data;
            node.type = 0;
            node.dataLen = dataLen;
            node.source = null;
            node.filename = Path.GetFileName(path);
            node.nameAddress = dataAddress;
            dataAddress += (uint)node.name.Length;
            node.dataAddress = dataAddress;
            dataAddress += (uint)node.data.Length;

            filelist.Add(node);
            return true;
        }

    }
}
