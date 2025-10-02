using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public class AudioPacketReader
    {
        private readonly string _filePath;

        public AudioPacketReader(string filePath)
        {
            _filePath = filePath;
        }

        public IEnumerable<AudioPacketMetadata> ReadAllPackets(CancellationToken cancellationToken = default)
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            while (fs.Position < fs.Length)
            {
                if (AudioPacketMetadata.TryReadMetadata(br, out var metadata) && metadata != null)
                {
                    yield return metadata;
                }
                else
                {
                    // Log or handle error if needed
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                    yield break;
            }
        }
    }
}