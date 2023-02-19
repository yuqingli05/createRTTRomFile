using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace createRomFs
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 3 && args.Length != 4)
            {
                Console.WriteLine("createRomFile.exe c://path c://file.bin 0x00");
                Console.WriteLine("createRomFile.exe c://path c://file.bin 0x00 c://link.cvs");
                return -1;
            }

            romfs romfs = new romfs();
            string link = null;
            string path = args[0];
            string bin = args[1];
            uint address = Convert.ToUInt32(args[2], 16);
            if(args.Length == 4)
                link = args[3];

            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo[] fil = dir.GetFiles();
            foreach (FileInfo f in fil)
            {
                if (!romfs.addFile(f.FullName))
                {
                    Console.WriteLine("保存文件失败,可能是权限不够");
                    return -1;
                }
            }

            if(link != null)
            {
                try
                {
                    using (System.IO.StreamReader file =new System.IO.StreamReader(link))
                    {
                        string line;
                        while ((line = file.ReadLine()) != null)
                        {
                            string[] listPath = line.Split(',');
                            if(listPath.Length >= 2)
                            {
                                if (!romfs.addLink(listPath[0].Trim(), listPath[1].Trim()))
                                {
                                    Console.WriteLine("链接失败, 请检查源文件是否存在<" + listPath[0].Trim() + ">");
                                    return -1;
                                }
                            }
                        }
                    }
                }
                catch (System.UnauthorizedAccessException ex)
                {
                    Console.WriteLine("保存文件失败,可能是权限不够");
                    return -1;
                }

            }

            if (!romfs.saveBin(bin, address))
            {
                Console.WriteLine("保存文件失败,可能是权限不够");
                return -1;
            }

            Console.WriteLine("生成 " + bin + " 成功");
            return 0;
        }
    }
}
