/* Copyright (c) 2011 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Gibbed.Helpers;
using Gibbed.LIMBO.FileFormats;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using NDesk.Options;

namespace Gibbed.LIMBO.Unpack
{
    internal class Program
    {
        private static string GetExecutableName()
        {
            return Path.GetFileName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
        }

        public static void Main(string[] args)
        {
            bool showHelp = false;
            bool extractUnknowns = true;
            bool overwriteFiles = false;
            bool verbose = false;

            var options = new OptionSet()
            {
                {
                    "o|overwrite",
                    "overwrite existing files",
                    v => overwriteFiles = v != null
                },
                {
                    "nu|no-unknowns",
                    "don't extract unknown files",
                    v => extractUnknowns = v == null
                },
                {
                    "v|verbose",
                    "be verbose",
                    v => verbose = v != null
                },
                {
                    "h|help",
                    "show this message and exit", 
                    v => showHelp = v != null
                },
            };

            List<string> extras;

            try
            {
                extras = options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.Write("{0}: ", GetExecutableName());
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
                return;
            }

            if (extras.Count < 1 || extras.Count > 2 || showHelp == true)
            {
                Console.WriteLine("Usage: {0} [OPTIONS]+ input_fat [output_dir]", GetExecutableName());
                Console.WriteLine();
                Console.WriteLine("Options:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            string inputPath = extras[0];
            string outputPath = extras.Count > 1 ? extras[1] : Path.ChangeExtension(inputPath, null) + "_unpack";

            var manager = ProjectData.Manager.Load();
            if (manager.ActiveProject == null)
            {
                Console.WriteLine("Warning: no active project loaded.");
            }

            var hashes = manager.LoadLists(
                "*.filelist",
                s => s.HashCRC32(),
                s => s
                    .Replace('\\', '/')
                    .ToLowerInvariant());

            using (var input = File.OpenRead(inputPath))
            {
                var package = new PackageFile();
                package.Deserialize(input);

                var baseOffset = input.Position;

                long current = 0;
                long total = package.Entries.Count;

                foreach (var entry in package.Entries)
                {
                    current++;

                    string name = hashes[entry.NameHash];
                    if (name == null)
                    {
                        if (extractUnknowns == false)
                        {
                            continue;
                        }

                        name = entry.NameHash.ToString("X8");
                        name = Path.Combine("__UNKNOWN", name);
                    }
                    else
                    {
                        name = name.Replace("/", "\\");
                        if (name.StartsWith("\\") == true)
                        {
                            name = name.Substring(1);
                        }
                    }

                    Stream data;
                    long length;

                    if (name.EndsWith(".d") == true)
                    {
                        try
                        {
                            data = new MemoryStream();
                            using (var compressed = input.ReadToMemoryStream(entry.Size))
                            {
                                var uncompressed = new InflaterInputStream(compressed);
                                var buffer = new byte[0x100000];

                                while (true)
                                {
                                    int read = uncompressed.Read(buffer, 0, buffer.Length);
                                    if (read <= 0)
                                    {
                                        throw new InvalidOperationException();
                                    }

                                    data.Write(buffer, 0, read);
                                    if (read < buffer.Length)
                                    {
                                        break;
                                    }
                                }

                                data.Position = 0;
                                length = data.Length;
                                name = name.Substring(0, name.Length - 2);
                            }
                        }
                        catch (ICSharpCode.SharpZipLib.SharpZipBaseException)
                        {
                            Console.WriteLine("failed to decompress '{0}' so it'll be left alone", name);

                            input.Seek(baseOffset + entry.Offset, SeekOrigin.Begin);
                            data = input;
                            length = entry.Size;
                        }
                    }
                    else
                    {
                        input.Seek(baseOffset + entry.Offset, SeekOrigin.Begin);
                        data = input;
                        length = entry.Size;
                    }

                    var entryPath = Path.Combine(outputPath, name);

                    if (overwriteFiles == false &&
                        File.Exists(entryPath) == true)
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(entryPath));

                    if (verbose == true)
                    {
                        Console.WriteLine("[{0}/{1}] {2}",
                            current, total, name);
                    }

                    using (var output = File.Create(entryPath))
                    {
                        output.WriteFromStream(data, length);
                    }
                }
            }
        }
    }
}
