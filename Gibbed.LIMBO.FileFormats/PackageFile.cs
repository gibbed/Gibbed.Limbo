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

namespace Gibbed.LIMBO.FileFormats
{
    public class PackageFile
    {
        public List<Package.Entry> Entries
            = new List<Package.Entry>();

        public void Serialize(Stream output)
        {
            output.WriteValueS32(this.Entries.Count);
            foreach (var entry in this.Entries)
            {
                output.WriteValueU32(entry.NameHash);
                output.WriteValueU32(entry.Offset);
                output.WriteValueU32(entry.Size);
            }
        }

        public void Deserialize(Stream input)
        {
            var count = input.ReadValueU32();
            if (input.Position + (count * 12) > input.Length)
            {
                throw new FormatException();
            }

            this.Entries.Clear();
            for (uint i = 0; i < count; i++)
            {
                var entry = new Package.Entry();
                entry.NameHash = input.ReadValueU32();
                entry.Offset = input.ReadValueU32();
                entry.Size = input.ReadValueU32();
                this.Entries.Add(entry);
            }
        }
    }
}
