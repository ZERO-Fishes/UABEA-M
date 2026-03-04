using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public class CommandLineHandler
    {
        public static void PrintHelp()
        {
            Console.WriteLine("UABE AVALONIA");
            Console.WriteLine("WARNING: Command line support VERY EARLY");
            Console.WriteLine("There is a high chance of stuff breaking");
            Console.WriteLine("Use at your own risk");
            Console.WriteLine("");
            Console.WriteLine("Commands:");
            Console.WriteLine("  UABEAvalonia batchexportbundle <input file/directory> [output directory]");
            Console.WriteLine("  UABEAvalonia batchimportbundle <directory>");
            Console.WriteLine("  UABEAvalonia batchexportassets <input path> [output directory]");
            Console.WriteLine("  UABEAvalonia applyemip <emip file> <directory>");
            Console.WriteLine("");
            Console.WriteLine("IMPORTANT: Bundle export always keeps original file names!");
            Console.WriteLine("           .assets files reference .resS by hardcoded filename.");
            Console.WriteLine("           Renaming will break resource references!");
            Console.WriteLine("");
            Console.WriteLine("Bundle export arguments (batchexportbundle):");
            Console.WriteLine("  Input:         Single bundle file OR directory containing bundles");
            Console.WriteLine("  -kd            Keep .decomp files after extraction");
            Console.WriteLine("  -md            Decompress into memory (no temp files)");
            Console.WriteLine("");
            Console.WriteLine("Bundle import arguments (batchimportbundle):");
            Console.WriteLine("  -keepnames     Use exact file name in bundle (not compatible with default export)");
            Console.WriteLine("  -kd            Keep .decomp files");
            Console.WriteLine("  -fd            Force overwrite old .decomp files");
            Console.WriteLine("  -md            Decompress into memory");
            Console.WriteLine("");
            Console.WriteLine("Assets export arguments (batchexportassets):");
            Console.WriteLine("  -recursive     Process directories recursively");
            Console.WriteLine("  -filter:<id>   Only export assets of specific type (e.g., 28 for Texture2D)");
            Console.WriteLine("  -raw           Export raw binary data (default)");
            Console.WriteLine("  -json          Export as JSON format");
            Console.WriteLine("  -dump          Export as text dump format");
            Console.WriteLine("  -frombundle    Also process bundle files in the directory");
            Console.WriteLine("  -nobundleassets Skip internal assets files when processing bundles");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  UABEAvalonia batchexportbundle C:\\Game\\Bundles C:\\Export");
            Console.WriteLine("  UABEAvalonia batchexportbundle C:\\Game\\Bundles -keepnames");
            Console.WriteLine("  UABEAvalonia batchexportassets C:\\Game\\resources.assets C:\\Export -json");
        }

        private static string GetMainFileName(string[] args)
        {
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                    return args[i];
            }
            return string.Empty;
        }

        private static HashSet<string> GetFlags(string[] args)
        {
            HashSet<string> flags = new HashSet<string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                    flags.Add(args[i]);
            }
            return flags;
        }

        private static AssetBundleFile DecompressBundle(string file, string? decompFile)
        {
            AssetBundleFile bun = new AssetBundleFile();

            Stream fs = File.OpenRead(file);
            AssetsFileReader r = new AssetsFileReader(fs);

            bun.Read(r);
            if (bun.Header.GetCompressionType() != 0)
            {
                Stream nfs;
                if (decompFile == null)
                    nfs = new MemoryStream();
                else
                    nfs = File.Open(decompFile, FileMode.Create, FileAccess.ReadWrite);

                AssetsFileWriter w = new AssetsFileWriter(nfs);
                bun.Unpack(w);

                nfs.Position = 0;
                fs.Close();

                fs = nfs;
                r = new AssetsFileReader(fs);

                bun = new AssetBundleFile();
                bun.Read(r);
            }

            return bun;
        }

        private static string GetNextBackup(string affectedFilePath)
        {
            for (int i = 0; i < 10000; i++)
            {
                string bakName = $"{affectedFilePath}.bak{i.ToString().PadLeft(4, '0')}";
                if (!File.Exists(bakName))
                {
                    return bakName;
                }
            }

            Console.WriteLine("Too many backups, exiting for your safety.");
            return null;
        }

        private static void BatchExportBundle(string[] args)
        {
            // 获取输入路径（文件或目录）
            string inputPath = GetMainFileName(args);
            if (string.IsNullOrEmpty(inputPath))
            {
                Console.WriteLine("Error: No input file or directory specified!");
                Console.WriteLine("Usage: UABEAvalonia batchexportbundle <input file or directory> [output directory] [options]");
                return;
            }

            // 判断输入是文件还是目录
            bool isSingleFile = File.Exists(inputPath);
            bool isDirectory = Directory.Exists(inputPath);

            if (!isSingleFile && !isDirectory)
            {
                Console.WriteLine($"Error: Input path '{inputPath}' does not exist!");
                return;
            }

            // 获取输出目录（第二个非标志参数）
            string outputDirectory = GetSecondFileName(args);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                // 默认输出到输入目录下的 exported 子文件夹，或与输入文件同目录
                if (isDirectory)
                    outputDirectory = Path.Combine(inputPath, "exported");
                else
                    outputDirectory = Path.Combine(Path.GetDirectoryName(inputPath)!, "exported");
            }

            // 创建输出目录
            Directory.CreateDirectory(outputDirectory);
            Console.WriteLine($"Input: {inputPath}");
            Console.WriteLine($"Output directory: {outputDirectory}");

            HashSet<string> flags = GetFlags(args);
            int processedBundles = 0;
            int totalFiles = 0;

            // 准备文件列表
            List<string> filesToProcess = new List<string>();
            if (isSingleFile)
            {
                filesToProcess.Add(inputPath);
            }
            else
            {
                filesToProcess.AddRange(Directory.EnumerateFiles(inputPath));
            }

            foreach (string file in filesToProcess)
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"Warning: File {file} does not exist, skipping...");
                    continue;
                }

                DetectedFileType fileType = FileTypeDetector.DetectFileType(file);
                if (fileType != DetectedFileType.BundleFile)
                {
                    if (isSingleFile)
                    {
                        Console.WriteLine($"Error: File '{file}' is not a valid Bundle file!");
                        return;
                    }
                    continue;
                }

                Console.WriteLine($"\nProcessing bundle: {Path.GetFileName(file)}");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
                Console.WriteLine($"  Found {entryCount} files");

                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, i);
                    
                    // 警告：必须保持原始文件名！
                    // .assets 文件通过硬编码的文件名引用 .resS 等资源文件
                    // 如果修改文件名，资源引用将失效！
                    string outName = Path.Combine(outputDirectory, name);
                    
                    // 确保输出目录存在（支持嵌套路径如 Data/SubFolder/file.assets）
                    string? outDir = Path.GetDirectoryName(outName);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }
                    
                    Console.WriteLine($"  Exporting {name}...");
                    File.WriteAllBytes(outName, data);
                    totalFiles++;
                }

                bun.Close();
                processedBundles++;

                if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                    File.Delete(decompFile);
            }

            Console.WriteLine($"\n========================================");
            Console.WriteLine($"Export completed!");
            Console.WriteLine($"Processed {processedBundles} bundles");
            Console.WriteLine($"Exported {totalFiles} files");
            Console.WriteLine($"Output directory: {outputDirectory}");
        }

        private static void BatchImportBundle(string[] args)
        {
            string importDirectory = GetMainFileName(args);
            if (!Directory.Exists(importDirectory))
            {
                Console.WriteLine("Directory does not exist!");
                return;
            }

            HashSet<string> flags = GetFlags(args);
            foreach (string file in Directory.EnumerateFiles(importDirectory))
            {
                string decompFile = $"{file}.decomp";

                if (flags.Contains("-md"))
                    decompFile = null;

                if (!File.Exists(file))
                {
                    Console.WriteLine($"File {file} does not exist!");
                    return;
                }

                DetectedFileType fileType = FileTypeDetector.DetectFileType(file);
                if (fileType != DetectedFileType.BundleFile)
                {
                    continue;
                }

                Console.WriteLine($"Decompressing {file} to {decompFile}...");
                AssetBundleFile bun = DecompressBundle(file, decompFile);

                List<BundleReplacer> reps = new List<BundleReplacer>();
                List<Stream> streams = new List<Stream>();

                int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;
                for (int i = 0; i < entryCount; i++)
                {
                    string name = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                    string matchName = Path.Combine(importDirectory, $"{Path.GetFileName(file)}_{name}");

                    if (File.Exists(matchName))
                    {
                        FileStream fs = File.OpenRead(matchName);
                        long length = fs.Length;
                        reps.Add(new BundleReplacerFromStream(name, name, true, fs, 0, length));
                        streams.Add(fs);
                        Console.WriteLine($"Importing {matchName}...");
                    }
                }

                //I guess uabe always writes to .decomp even if
                //the bundle is already decompressed, that way
                //here it can be used as a temporary file. for
                //now I'll write to memory since having a .decomp
                //file isn't guaranteed here
                byte[] data;
                using (MemoryStream ms = new MemoryStream())
                using (AssetsFileWriter w = new AssetsFileWriter(ms))
                {
                    bun.Write(w, reps);
                    data = ms.ToArray();
                }
                Console.WriteLine($"Writing changes to {file}...");

                //uabe doesn't seem to compress here

                foreach (Stream stream in streams)
                    stream.Close();

                bun.Close();

                File.WriteAllBytes(file, data);

                if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                    File.Delete(decompFile);

                Console.WriteLine("Done.");
            }
        }
        
        private static void ApplyEmip(string[] args)
        {
            HashSet<string> flags = GetFlags(args);
            string emipFile = args[1];
            string rootDir = args[2];

            if (!File.Exists(emipFile))
            {
                Console.WriteLine($"File {emipFile} does not exist!");
                return;
            }

            InstallerPackageFile instPkg = new InstallerPackageFile();
            FileStream fs = File.OpenRead(emipFile);
            AssetsFileReader r = new AssetsFileReader(fs);
            instPkg.Read(r, true);

            Console.WriteLine($"Installing emip...");
            Console.WriteLine($"{instPkg.modName} by {instPkg.modCreators}");
            Console.WriteLine(instPkg.modDescription);

            foreach (var affectedFile in instPkg.affectedFiles)
            {
                string affectedFileName = Path.GetFileName(affectedFile.path);
                string affectedFilePath = Path.Combine(rootDir, affectedFile.path);

                if (affectedFile.isBundle)
                {
                    string decompFile = $"{affectedFilePath}.decomp";
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                        return;

                    if (flags.Contains("-md"))
                        decompFile = null;

                    Console.WriteLine($"Decompressing {affectedFileName} to {decompFile??"memory"}...");
                    AssetBundleFile bun = DecompressBundle(affectedFilePath, decompFile);
                    List<BundleReplacer> reps = new List<BundleReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var bunRep = (BundleReplacer)rep;
                        if (bunRep is BundleReplacerFromAssets)
                        {
                            //read in assets files from the bundle for replacers that need them
                            string assetName = bunRep.GetOriginalEntryName();
                            var bunRepInf = BundleHelper.GetDirInfo(bun, assetName);
                            long pos = bunRepInf.Offset;
                            bunRep.Init(bun.DataReader, pos, bunRepInf.DecompressedSize);
                        }
                        reps.Add(bunRep);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    FileStream mfs = File.Open(modFile, FileMode.Create);
                    AssetsFileWriter mw = new AssetsFileWriter(mfs);
                    bun.Write(mw, reps, instPkg.addedTypes); //addedTypes does nothing atm
                    
                    mfs.Close();
                    bun.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);
                    
                    if (!flags.Contains("-kd") && !flags.Contains("-md") && File.Exists(decompFile))
                        File.Delete(decompFile);

                    Console.WriteLine($"Done.");
                }
                else //isAssetsFile
                {
                    string modFile = $"{affectedFilePath}.mod";
                    string bakFile = GetNextBackup(affectedFilePath);

                    if (bakFile == null)
                        return;

                    FileStream afs = File.OpenRead(affectedFilePath);
                    AssetsFileReader ar = new AssetsFileReader(afs);
                    AssetsFile assets = new AssetsFile();
                    assets.Read(ar);
                    List<AssetsReplacer> reps = new List<AssetsReplacer>();

                    foreach (var rep in affectedFile.replacers)
                    {
                        var assetsReplacer = (AssetsReplacer)rep;
                        reps.Add(assetsReplacer);
                    }

                    Console.WriteLine($"Writing {modFile}...");
                    FileStream mfs = File.Open(modFile, FileMode.Create);
                    AssetsFileWriter mw = new AssetsFileWriter(mfs);
                    assets.Write(mw, 0, reps, instPkg.addedTypes);

                    mfs.Close();
                    ar.Close();

                    Console.WriteLine($"Swapping mod file...");
                    File.Move(affectedFilePath, bakFile);
                    File.Move(modFile, affectedFilePath);

                    Console.WriteLine($"Done.");
                }
            }

            return;
        }

        private static void BatchExportAssets(string[] args)
        {
            // 检查是否是帮助请求
            if (args.Length >= 2 && (args[1] == "--help" || args[1] == "-h" || args[1] == "/?"))
            {
                Console.WriteLine("UABEA Batch Export Assets - 批量导出资源");
                Console.WriteLine("");
                Console.WriteLine("用法: UABEAvalonia batchexportassets <输入路径> [输出目录] [选项]");
                Console.WriteLine("");
                Console.WriteLine("参数:");
                Console.WriteLine("  <输入路径>          要处理的 .assets 文件或文件夹路径（必需）");
                Console.WriteLine("  [输出目录]          导出结果保存位置（可选，默认: 输入路径/exported）");
                Console.WriteLine("");
                Console.WriteLine("选项:");
                Console.WriteLine("  -recursive          递归处理子文件夹");
                Console.WriteLine("  -filter:<classId>   只导出指定类型的资源（如 -filter:28 只导出 Texture2D）");
                Console.WriteLine("  -raw                导出原始二进制数据（默认）");
                Console.WriteLine("  -json               导出为 JSON 格式");
                Console.WriteLine("  -dump               导出为文本格式（UABE dump格式）");
                Console.WriteLine("  -frombundle         同时处理文件夹中的 bundle 文件");
                Console.WriteLine("  -nobundleassets     从 bundle 导出时跳过内部的 assets 文件");
                Console.WriteLine("");
                Console.WriteLine("示例:");
                Console.WriteLine("  UABEAvalonia batchexportassets C:\\Game\\resources.assets C:\\Export");
                Console.WriteLine("  UABEAvalonia batchexportassets C:\\Game C:\\Export -recursive -filter:28 -json");
                Console.WriteLine("");
                Console.WriteLine("常用资源类型 ID:");
                Console.WriteLine("  1=GameObject, 28=Texture2D, 48=Mesh, 49=TextAsset,");
                Console.WriteLine("  83=AudioClip, 128=Font, 142=Sprite");
                return;
            }

            string inputPath = GetMainFileName(args);
            if (string.IsNullOrEmpty(inputPath))
            {
                Console.WriteLine("Error: No input file or directory specified!");
                Console.WriteLine("提示: 使用 --help 查看详细用法");
                return;
            }

            if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input path '{inputPath}' does not exist!");
                return;
            }

            // 获取输出目录（第二个非标志参数）
            string outputDirectory = GetSecondFileName(args);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                // 默认输出到输入目录下的 exported 文件夹
                if (Directory.Exists(inputPath))
                    outputDirectory = Path.Combine(inputPath, "exported");
                else
                    outputDirectory = Path.Combine(Path.GetDirectoryName(inputPath)!, "exported");
            }

            Directory.CreateDirectory(outputDirectory);
            Console.WriteLine($"Output directory: {outputDirectory}");

            HashSet<string> flags = GetFlags(args);
            bool recursive = flags.Contains("-recursive");
            bool fromBundle = flags.Contains("-frombundle");
            bool noBundleAssets = flags.Contains("-nobundleassets");
            
            // 解析导出格式
            bool exportJson = flags.Contains("-json");
            bool exportDump = flags.Contains("-dump");
            bool exportRaw = !exportJson && !exportDump; // 默认为 raw
            
            // 解析类型过滤
            int? filterClassId = null;
            foreach (var flag in flags)
            {
                if (flag.StartsWith("-filter:"))
                {
                    if (int.TryParse(flag.Substring(8), out int classId))
                    {
                        filterClassId = classId;
                        Console.WriteLine($"Filtering by class ID: {classId}");
                    }
                }
            }

            // 收集要处理的文件
            List<string> assetsFiles = new List<string>();
            List<string> bundleFiles = new List<string>();

            if (File.Exists(inputPath))
            {
                DetectedFileType fileType = FileTypeDetector.DetectFileType(inputPath);
                if (fileType == DetectedFileType.AssetsFile)
                    assetsFiles.Add(inputPath);
                else if (fileType == DetectedFileType.BundleFile && fromBundle)
                    bundleFiles.Add(inputPath);
            }
            else // Directory
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                // 查找 assets 文件
                foreach (var file in Directory.EnumerateFiles(inputPath, "*.assets", searchOption))
                {
                    if (FileTypeDetector.DetectFileType(file) == DetectedFileType.AssetsFile)
                        assetsFiles.Add(file);
                }
                
                // 查找 bundle 文件（如果启用）
                if (fromBundle)
                {
                    foreach (var file in Directory.EnumerateFiles(inputPath, "*.bundle", searchOption))
                    {
                        if (FileTypeDetector.DetectFileType(file) == DetectedFileType.BundleFile)
                            bundleFiles.Add(file);
                    }
                    // 也检查无扩展名的文件（有些 bundle 没有扩展名）
                    foreach (var file in Directory.EnumerateFiles(inputPath, "*", searchOption))
                    {
                        if (!file.EndsWith(".bundle") && !file.EndsWith(".assets"))
                        {
                            try
                            {
                                if (FileTypeDetector.DetectFileType(file) == DetectedFileType.BundleFile)
                                    bundleFiles.Add(file);
                            }
                            catch { /* ignore */ }
                        }
                    }
                }
            }

            Console.WriteLine($"Found {assetsFiles.Count} assets files to process");
            if (fromBundle)
                Console.WriteLine($"Found {bundleFiles.Count} bundle files to process");

            // 初始化 AssetsManager
            var manager = new AssetsManager();
            int totalExported = 0;

            // 处理 Assets 文件
            foreach (var assetsFile in assetsFiles)
            {
                Console.WriteLine($"\nProcessing assets file: {assetsFile}");
                try
                {
                    int exported = ProcessAssetsFile(assetsFile, outputDirectory, manager, filterClassId, exportRaw, exportJson, exportDump);
                    totalExported += exported;
                    Console.WriteLine($"  Exported {exported} assets");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: {ex.Message}");
                }
            }

            // 处理 Bundle 文件
            if (fromBundle)
            {
                foreach (var bundleFile in bundleFiles)
                {
                    Console.WriteLine($"\nProcessing bundle file: {bundleFile}");
                    try
                    {
                        int exported = ProcessBundleForAssets(bundleFile, outputDirectory, manager, filterClassId, exportRaw, exportJson, exportDump, noBundleAssets);
                        totalExported += exported;
                        Console.WriteLine($"  Exported {exported} assets");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Error: {ex.Message}");
                    }
                }
            }

            Console.WriteLine($"\n========================================");
            Console.WriteLine($"Batch export completed!");
            Console.WriteLine($"Total assets exported: {totalExported}");
            Console.WriteLine($"Output directory: {outputDirectory}");
        }

        private static int ProcessAssetsFile(string filePath, string outputDirectory, AssetsManager manager, 
            int? filterClassId, bool exportRaw, bool exportJson, bool exportDump)
        {
            int exportedCount = 0;
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // 创建该文件的输出子目录
            string fileOutputDir = Path.Combine(outputDirectory, $"{fileName}_export");
            Directory.CreateDirectory(fileOutputDir);

            // 使用 AssetWorkspace 管理资源
            var workspace = new AssetWorkspace(manager, false);
            
            // 加载 assets 文件
            var assetsInst = manager.LoadAssetsFile(filePath, true);
            if (assetsInst == null)
            {
                Console.WriteLine($"  Failed to load assets file");
                return 0;
            }

            workspace.LoadAssetsFile(assetsInst, false);

            // 遍历所有资源
            foreach (var assetInfo in assetsInst.file.AssetInfos)
            {
                // 类型过滤
                if (filterClassId.HasValue && assetInfo.TypeId != filterClassId.Value)
                    continue;

                try
                {
                    string assetName = $"{assetInfo.PathId}";
                    string className = ((AssetClassID)assetInfo.TypeId).ToString();

                    // 创建 AssetContainer 用于获取 BaseField
                    var container = new AssetContainer(assetInfo, assetsInst);
                    
                    // 获取 BaseField（这会解析资源数据）
                    var baseField = workspace.GetBaseField(container);
                    if (baseField != null)
                    {
                        var nameField = baseField["m_Name"];
                        if (nameField != null && !string.IsNullOrEmpty(nameField.AsString))
                        {
                            assetName = $"{assetInfo.PathId}_{nameField.AsString}";
                        }
                    }

                    // 清理文件名
                    assetName = SanitizeFileName(assetName);
                    string extension = exportJson ? ".json" : (exportDump ? ".txt" : ".bin");
                    string outputPath = Path.Combine(fileOutputDir, $"{assetName}_{className}{extension}");

                    // 导出数据
                    if (exportRaw)
                    {
                        // 原始二进制导出
                        var reader = assetsInst.file.Reader;
                        reader.Position = assetInfo.AbsoluteByteStart;
                        byte[] data = reader.ReadBytes((int)assetInfo.ByteSize);
                        File.WriteAllBytes(outputPath, data);
                    }
                    else if (exportJson || exportDump)
                    {
                        // 使用已获取的 baseField
                        if (baseField != null)
                        {
                            var importExport = new AssetImportExport();
                            using (var sw = new StreamWriter(outputPath))
                            {
                                if (exportJson)
                                    importExport.DumpJsonAsset(sw, baseField);
                                else
                                    importExport.DumpTextAsset(sw, baseField);
                            }
                        }
                    }

                    exportedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    Warning: Failed to export asset {assetInfo.PathId}: {ex.Message}");
                }
            }

            assetsInst.file.Close();
            return exportedCount;
        }

        private static int ProcessBundleForAssets(string bundleFile, string outputDirectory, AssetsManager manager,
            int? filterClassId, bool exportRaw, bool exportJson, bool exportDump, bool noBundleAssets)
        {
            int exportedCount = 0;
            string bundleName = Path.GetFileName(bundleFile);
            string bundleOutputDir = Path.Combine(outputDirectory, $"{bundleName}_export");
            Directory.CreateDirectory(bundleOutputDir);

            // 解压 bundle
            AssetBundleFile bun = DecompressBundle(bundleFile, null);
            int entryCount = bun.BlockAndDirInfo.DirectoryInfos.Length;

            // 分类文件：Assets 文件 和 资源文件 (.resS, .resource 等)
            List<(string name, int index)> assetsFiles = new List<(string, int)>();
            List<(string name, int index)> resourceFiles = new List<(string, int)>();

            for (int i = 0; i < entryCount; i++)
            {
                string entryName = bun.BlockAndDirInfo.DirectoryInfos[i].Name;
                bool isAssetsFile = entryName.EndsWith(".assets") || entryName.EndsWith(".asset");
                bool isResourceFile = entryName.EndsWith(".resS") || entryName.EndsWith(".resource");
                
                if (isAssetsFile)
                    assetsFiles.Add((entryName, i));
                else
                    resourceFiles.Add((entryName, i));
            }

            // 如果要解析资源数据（JSON/Dump 导出），需要同时提取 .resS 文件到同一目录
            bool needParseResources = exportJson || exportDump;
            string? tempWorkDir = null;

            if (needParseResources && assetsFiles.Count > 0)
            {
                // 创建临时工作目录，将 .assets 和 .resS 放在同一目录
                tempWorkDir = Path.Combine(Path.GetTempPath(), $"uabea_bundle_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempWorkDir);

                // 提取所有资源文件到临时目录
                foreach (var (name, index) in resourceFiles)
                {
                    try
                    {
                        byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, index);
                        string tempPath = Path.Combine(tempWorkDir, name);
                        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
                        File.WriteAllBytes(tempPath, data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Failed to extract resource {name}: {ex.Message}");
                    }
                }
            }

            // 处理 Assets 文件
            foreach (var (entryName, index) in assetsFiles)
            {
                if (noBundleAssets)
                    continue;

                try
                {
                    byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, index);
                    
                    if (needParseResources && tempWorkDir != null)
                    {
                        // JSON/Dump 导出：需要解析资源数据，使用临时目录保持文件关联
                        string tempAssetsPath = Path.Combine(tempWorkDir, entryName);
                        File.WriteAllBytes(tempAssetsPath, data);
                        
                        try
                        {
                            exportedCount += ProcessAssetsFile(tempAssetsPath, bundleOutputDir, manager, filterClassId, exportRaw, exportJson, exportDump);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Warning: Failed to process {entryName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Raw 导出：直接保存原始文件
                        string outName = Path.Combine(bundleOutputDir, entryName);
                        File.WriteAllBytes(outName, data);
                        exportedCount++;
                        
                        // 同时导出关联的 .resS 文件（如果存在）
                        string baseName = Path.GetFileNameWithoutExtension(entryName);
                        foreach (var (resName, resIndex) in resourceFiles)
                        {
                            if (resName.StartsWith(baseName) || entryName.StartsWith(Path.GetFileNameWithoutExtension(resName)))
                            {
                                try
                                {
                                    byte[] resData = BundleHelper.LoadAssetDataFromBundle(bun, resIndex);
                                    string resOutName = Path.Combine(bundleOutputDir, resName);
                                    File.WriteAllBytes(resOutName, resData);
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Failed to process bundle entry {entryName}: {ex.Message}");
                }
            }

            // 清理临时目录
            if (tempWorkDir != null && Directory.Exists(tempWorkDir))
            {
                try
                {
                    Directory.Delete(tempWorkDir, true);
                }
                catch { }
            }

            // 导出剩余的非 Assets 文件（仅在 Raw 模式或未解析资源时）
            if (!needParseResources)
            {
                foreach (var (entryName, index) in resourceFiles)
                {
                    // 检查是否已经导出
                    string outName = Path.Combine(bundleOutputDir, entryName);
                    if (File.Exists(outName))
                        continue;

                    try
                    {
                        byte[] data = BundleHelper.LoadAssetDataFromBundle(bun, index);
                        Directory.CreateDirectory(Path.GetDirectoryName(outName)!);
                        File.WriteAllBytes(outName, data);
                        exportedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Warning: Failed to export {entryName}: {ex.Message}");
                    }
                }
            }

            bun.Close();
            return exportedCount;
        }

        private static string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            // 限制长度
            if (fileName.Length > 100)
                fileName = fileName.Substring(0, 100);
            return fileName;
        }

        private static string GetSecondFileName(string[] args)
        {
            int fileCount = 0;
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-"))
                {
                    fileCount++;
                    if (fileCount == 2)
                        return args[i];
                }
            }
            return string.Empty;
        }

        public static void CLHMain(string[] args)
        {
            if (args.Length < 1)
            {
                PrintHelp();
                return;
            }
            
            string command = args[0];

            if (command == "batchexportbundle")
            {
                if (args.Length < 2)
                {
                    PrintHelp();
                    return;
                }
                BatchExportBundle(args);
            }
            else if (command == "batchimportbundle")
            {
                if (args.Length < 2)
                {
                    PrintHelp();
                    return;
                }
                BatchImportBundle(args);
            }
            else if (command == "batchexportassets")
            {
                if (args.Length < 2)
                {
                    PrintHelp();
                    return;
                }
                BatchExportAssets(args);
            }
            else if (command == "applyemip")
            {
                if (args.Length < 3)
                {
                    PrintHelp();
                    return;
                }
                ApplyEmip(args);
            }
            else
            {
                PrintHelp();
            }
        }
    }
}