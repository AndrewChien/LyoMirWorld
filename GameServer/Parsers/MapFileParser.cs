using System;
using System.IO;
using MirCommon.Utils;

namespace GameServer.Parsers
{
    /// <summary>
    /// 地图文件格式 - 地图文件通常是二进制格式
    /// 包含 .map (物理地图) 和 .nmp (网格地图) 格式
    /// </summary>
    public class MapFileParser
    {
        /// <summary>
        /// 地图数据
        /// </summary>
        public class MapData
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public byte[,]? Tiles { get; set; }  // 地砖数据
            public byte[,]? Blocks { get; set; } // 阻挡数据
            public string FileName { get; set; } = "";
        }

        /// <summary>
        /// 加载.map格式地图文件（二进制格式）
        /// 文件格式：
        /// - 4字节：数据偏移量(dwTemp)
        /// - 4字节：宽度(重复读取两次,第二次才是真实宽度)
        /// - 4字节：高度
        /// - 跳转到偏移位置后，逐个读取格子数据：
        ///   每个格子1字节flag:
        ///   bit0=阻挡, bit1=读2字节, bit2=读2字节, bit3=读4字节
        ///   bit4=读1字节, bit5=读1字节, bit6=读1字节, bit7=读1字节
        /// </summary>
        public MapData? LoadMapFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"地图文件不存在: {filePath}");
                return null;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                var mapData = new MapData
                {
                    FileName = Path.GetFileNameWithoutExtension(filePath)
                };

                // 读取数据偏移量
                uint dataOffset = br.ReadUInt32();
                
                // 读取宽度（读取两次，第二次才是真实宽度）
                br.ReadInt32(); // 跳过第一次
                mapData.Width = br.ReadInt32();
                
                // 读取高度
                mapData.Height = br.ReadInt32();

                // 计算阻挡层需要的元素数量
                int totalCells = mapData.Width * mapData.Height;
                int maxBlockElements = (totalCells + 31) / 32;
                uint[] blockLayer = new uint[maxBlockElements];

                // 跳转到数据偏移位置
                fs.Seek(dataOffset, SeekOrigin.Begin);

                // 读取每个格子的数据
                int elemIndex = 0;
                int bitIndex = 0;
                int remainingCells = totalCells;

                while (remainingCells > 0)
                {
                    byte flag = br.ReadByte();

                    // bit 0: 阻挡标志
                    if ((flag & 1) != 0)
                    {
                        blockLayer[elemIndex] |= (uint)(1 << bitIndex);
                    }

                    // bit 1: 读取2字节
                    if ((flag & 2) != 0)
                        br.ReadUInt16();

                    // bit 2: 读取2字节
                    if ((flag & 4) != 0)
                        br.ReadUInt16();

                    // bit 3: 读取4字节
                    if ((flag & 8) != 0)
                        br.ReadUInt32();

                    // bit 4: 读取1字节
                    if ((flag & 16) != 0)
                        br.ReadByte();

                    // bit 5: 读取1字节
                    if ((flag & 32) != 0)
                        br.ReadByte();

                    // bit 6: 读取1字节
                    if ((flag & 64) != 0)
                        br.ReadByte();

                    // bit 7: 读取1字节
                    if ((flag & 128) != 0)
                        br.ReadByte();

                    bitIndex++;
                    if (bitIndex >= 32)
                    {
                        bitIndex = 0;
                        elemIndex++;
                        if (elemIndex >= maxBlockElements)
                            break;
                    }

                    remainingCells--;
                }

                // 将位图转换为二维数组便于使用
                mapData.Blocks = new byte[mapData.Width, mapData.Height];
                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        int cellIndex = y * mapData.Width + x;
                        int arrayIndex = cellIndex / 32;
                        int bitPos = cellIndex % 32;
                        
                        if ((blockLayer[arrayIndex] & (1 << bitPos)) != 0)
                        {
                            mapData.Blocks[x, y] = 1; // 阻挡
                        }
                        else
                        {
                            mapData.Blocks[x, y] = 0; // 可通行
                        }
                    }
                }

                LogManager.Default.Info($"成功加载地图: {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                return mapData;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图文件失败: {filePath}", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 加载.nmp格式地图文件
        /// NMP格式（网格地图格式）：
        /// - 4字节：宽度
        /// - 4字节：高度
        /// - 后续数据：每个格子1字节的阻挡信息（0=可通行，1=阻挡）
        /// 或者可能是位图格式：
        /// - 4字节：宽度
        /// - 4字节：高度
        /// - 后续数据：位图数据，每个格子1位
        /// </summary>
        public MapData? LoadNMPFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LogManager.Default.Warning($"NMP地图文件不存在: {filePath}");
                return null;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                var mapData = new MapData
                {
                    FileName = Path.GetFileNameWithoutExtension(filePath)
                };

                // 尝试读取宽度和高度
                mapData.Width = br.ReadInt32();
                mapData.Height = br.ReadInt32();

                // 计算总格子数
                int totalCells = mapData.Width * mapData.Height;
                
                // 计算文件剩余字节数
                long remainingBytes = fs.Length - fs.Position;
                
                // 根据剩余字节数判断格式
                if (remainingBytes == totalCells)
                {
                    // 格式1：每个格子1字节
                    mapData.Blocks = new byte[mapData.Width, mapData.Height];
                    for (int y = 0; y < mapData.Height; y++)
                    {
                        for (int x = 0; x < mapData.Width; x++)
                        {
                            byte block = br.ReadByte();
                            mapData.Blocks[x, y] = block != 0 ? (byte)1 : (byte)0;
                        }
                    }
                    LogManager.Default.Info($"加载NMP地图(字节格式): {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                }
                else if (remainingBytes == (totalCells + 7) / 8)
                {
                    // 格式2：位图格式，每个格子1位
                    mapData.Blocks = new byte[mapData.Width, mapData.Height];
                    int byteIndex = 0;
                    int bitIndex = 0;
                    byte currentByte = 0;
                    
                    for (int y = 0; y < mapData.Height; y++)
                    {
                        for (int x = 0; x < mapData.Width; x++)
                        {
                            if (bitIndex == 0)
                            {
                                currentByte = br.ReadByte();
                            }
                            
                            mapData.Blocks[x, y] = (currentByte & (1 << bitIndex)) != 0 ? (byte)1 : (byte)0;
                            
                            bitIndex++;
                            if (bitIndex >= 8)
                            {
                                bitIndex = 0;
                                byteIndex++;
                            }
                        }
                    }
                    LogManager.Default.Info($"加载NMP地图(位图格式): {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                }
                else
                {
                    // 格式3：可能包含其他信息的格式
                    // 假设前4字节是数据偏移量（类似.map格式）
                    fs.Seek(0, SeekOrigin.Begin);
                    uint dataOffset = br.ReadUInt32();
                    mapData.Width = br.ReadInt32();
                    mapData.Height = br.ReadInt32();
                    
                    // 跳转到数据位置
                    fs.Seek(dataOffset, SeekOrigin.Begin);
                    
                    totalCells = mapData.Width * mapData.Height;
                    mapData.Blocks = new byte[mapData.Width, mapData.Height];
                    
                    // 读取每个格子的阻挡信息
                    for (int i = 0; i < totalCells; i++)
                    {
                        byte flag = br.ReadByte();
                        int x = i % mapData.Width;
                        int y = i / mapData.Width;
                        mapData.Blocks[x, y] = (flag & 1) != 0 ? (byte)1 : (byte)0;
                        
                        // 跳过其他数据（如果有）
                        if ((flag & 2) != 0) br.ReadBytes(2);
                        if ((flag & 4) != 0) br.ReadBytes(2);
                        if ((flag & 8) != 0) br.ReadBytes(4);
                        if ((flag & 16) != 0) br.ReadByte();
                        if ((flag & 32) != 0) br.ReadByte();
                        if ((flag & 64) != 0) br.ReadByte();
                        if ((flag & 128) != 0) br.ReadByte();
                    }
                    LogManager.Default.Info($"加载NMP地图(扩展格式): {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                }

                return mapData;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载NMP地图文件失败: {filePath}", exception: ex);
                return null;
            }
        }

        /// <summary>
        /// 保存地图缓存
        /// 缓存格式(.PMC):
        /// - 4字节: 魔术头 "DMC0"
        /// - 4字节: 宽度
        /// - 4字节: 高度
        /// - 4字节: 阻挡数组元素数量
        /// - N*4字节: 阻挡位图数据
        /// </summary>
        public bool SaveMapCache(MapData mapData, string cachePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(cachePath) ?? "";
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var fs = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
                using var bw = new BinaryWriter(fs);

                // 写入魔术头 "DMC0"
                bw.Write((byte)'D');
                bw.Write((byte)'M');
                bw.Write((byte)'C');
                bw.Write((byte)'0');

                // 写入地图尺寸
                bw.Write(mapData.Width);
                bw.Write(mapData.Height);

                // 计算并写入阻挡数组大小
                int totalCells = mapData.Width * mapData.Height;
                int maxBlockElements = (totalCells + 31) / 32;
                bw.Write(maxBlockElements);

                // 将二维阻挡数组转换回位图格式
                uint[] blockLayer = new uint[maxBlockElements];
                if (mapData.Blocks != null)
                {
                    for (int y = 0; y < mapData.Height; y++)
                    {
                        for (int x = 0; x < mapData.Width; x++)
                        {
                            if (mapData.Blocks[x, y] != 0)
                            {
                                int cellIndex = y * mapData.Width + x;
                                int arrayIndex = cellIndex / 32;
                                int bitPos = cellIndex % 32;
                                blockLayer[arrayIndex] |= (uint)(1 << bitPos);
                            }
                        }
                    }
                }

                // 写入阻挡位图
                foreach (var block in blockLayer)
                {
                    bw.Write(block);
                }

                LogManager.Default.Info($"成功保存地图缓存: {cachePath}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"保存地图缓存失败: {cachePath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载地图缓存
        /// 缓存格式(.PMC):
        /// - 4字节: 魔术头 "DMC0"
        /// - 4字节: 宽度
        /// - 4字节: 高度
        /// - 4字节: 阻挡数组元素数量
        /// - N*4字节: 阻挡位图数据
        /// </summary>
        public MapData? LoadMapCache(string cachePath)
        {
            if (!File.Exists(cachePath))
                return null;

            try
            {
                using var fs = new FileStream(cachePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                // 读取并验证魔术头
                byte[] magic = br.ReadBytes(4);
                if (magic[0] != 'D' || magic[1] != 'M' || magic[2] != 'C' || magic[3] != '0')
                {
                    LogManager.Default.Warning($"无效的地图缓存文件: {cachePath}");
                    return null;
                }

                var mapData = new MapData
                {
                    FileName = Path.GetFileNameWithoutExtension(cachePath)
                };

                // 读取地图尺寸
                mapData.Width = br.ReadInt32();
                mapData.Height = br.ReadInt32();

                // 读取阻挡数组大小
                int maxBlockElements = br.ReadInt32();

                // 验证数据合法性
                int expectedElements = (mapData.Width * mapData.Height + 31) / 32;
                if (maxBlockElements < expectedElements)
                {
                    maxBlockElements = expectedElements;
                }

                // 读取阻挡位图
                uint[] blockLayer = new uint[maxBlockElements];
                for (int i = 0; i < maxBlockElements; i++)
                {
                    blockLayer[i] = br.ReadUInt32();
                }

                // 转换为二维数组
                mapData.Blocks = new byte[mapData.Width, mapData.Height];
                for (int y = 0; y < mapData.Height; y++)
                {
                    for (int x = 0; x < mapData.Width; x++)
                    {
                        int cellIndex = y * mapData.Width + x;
                        int arrayIndex = cellIndex / 32;
                        int bitPos = cellIndex % 32;
                        
                        if (arrayIndex < blockLayer.Length &&
                            (blockLayer[arrayIndex] & (1 << bitPos)) != 0)
                        {
                            mapData.Blocks[x, y] = 1; // 阻挡
                        }
                        else
                        {
                            mapData.Blocks[x, y] = 0; // 可通行
                        }
                    }
                }

                LogManager.Default.Info($"成功加载地图缓存: {mapData.FileName} ({mapData.Width}x{mapData.Height})");
                return mapData;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载地图缓存失败: {cachePath}", exception: ex);
                return null;
            }
        }
    }

    /// <summary>
    /// 逻辑地图配置解析器
    /// 读取逻辑地图的配置文件
    /// </summary>
    public class LogicMapConfigParser
    {
        public class LogicMapConfig
        {
            public int MapID { get; set; }
            public string MapName { get; set; } = "";
            public string PhysicsMapFile { get; set; } = "";
            public string MiniMapFile { get; set; } = "";
            public int MinX { get; set; }
            public int MinY { get; set; }
            public int MaxX { get; set; }
            public int MaxY { get; set; }
        }

        private readonly System.Collections.Generic.List<LogicMapConfig> _maps = new();

        /// <summary>
        /// 加载逻辑地图配置目录
        /// </summary>
        public bool LoadMapConfigs(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                LogManager.Default.Warning($"逻辑地图配置目录不存在: {directoryPath}");
                return false;
            }

            try
            {
                var files = Directory.GetFiles(directoryPath, "*.txt");
                int count = 0;

                foreach (var file in files)
                {
                    if (LoadMapConfig(file))
                        count++;
                }

                LogManager.Default.Info($"成功加载 {count} 个逻辑地图配置");
                return count > 0;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载逻辑地图配置失败: {directoryPath}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 加载单个地图配置文件
        /// </summary>
        private bool LoadMapConfig(string filePath)
        {
            try
            {
                var parser = new TextFileParser();
                var kvPairs = parser.LoadKeyValue(filePath);

                var config = new LogicMapConfig();
                
                if (kvPairs.TryGetValue("MapID", out string? mapId))
                    config.MapID = int.Parse(mapId);
                if (kvPairs.TryGetValue("MapName", out string? mapName))
                    config.MapName = mapName;
                if (kvPairs.TryGetValue("PhysicsMap", out string? physicsMap))
                    config.PhysicsMapFile = physicsMap;

                _maps.Add(config);
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Debug($"加载地图配置失败: {filePath} - {ex.Message}");
                return false;
            }
        }

        public System.Collections.Generic.IEnumerable<LogicMapConfig> GetAllMaps() => _maps;
    }
}
