# createRTTRomFile
创建 rtthread rom文件系统的二进制文件。用于单独下载文件系统

# 软件说明
~~~
1、rtthread 有一个只读文件系统，直接编译到单片机内部flash上的。这个软件用于生成这个只度文件系统。目前只支持32位小端结构的芯片。
2、使用文件夹生成文件系统
3、功能和 rtthread的工具 mkfomfs.py 类似。但是这个是单文件控制台命令,方便集成到上位机软件
~~~
# 原理
~~~
将文件模拟 gcc小端模式进行，二进制化。
~~~
# 使用方式
~~~
createRomFile.exe rompath target address

createRomFile.exe：本工程生产的可执行文件
rompath：需要生产文件系统的文件目录
target：目标文件
address：文件系统在单片机片上flash的地址（必须要对）

使用案例 .\createRomFs.exe .\resources .\resources.bin 0xC3000
注意：生产的文件是二进制文件bin文件，为了方便下载可以转化为hex文件。本人还有一个hex和bin文件的编辑器 https://github.com/yuqingli05/HexEdit
~~~
# rtthread 使用示例
~~~c
#include <rtthread.h>
#include <dfs_fs.h>
#include <dfs_romfs.h>
#include <stdint.h>

static const struct romfs_dirent _romfs_root[] = {
	{ROMFS_DIRENT_DIR, "resources", NULL, 0},
	{ROMFS_DIRENT_DIR, "dev", NULL, 0},
};

static const struct romfs_dirent _romfs = {ROMFS_DIRENT_DIR, "/", (rt_uint8_t *)_romfs_root, sizeof(_romfs_root) / sizeof(_romfs_root[0])};

int rt_hw_romfs_init(void)
{
	const struct romfs_dirent *res_romfs = (const struct romfs_dirent *)0xC3000;

	// 挂载根路径
	dfs_mount(RT_NULL, "/", "rom", 0, &_romfs);

	// 验证数据合法性 
	// 挂载只读资源文件
	if (res_romfs->type == ROMFS_DIRENT_DIR)
	{
		dfs_mount(RT_NULL, "/resources/", "rom", 0, res_romfs);
	}

	return 0;
}
INIT_ENV_EXPORT(rt_hw_romfs_init);
~~~
