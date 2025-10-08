using System;
using System.IO;
using System.Text;
using NLog;

namespace ShalevOhad.DCS.SRS.Recorder.Core
{
    public record AudioPacketMetadata(
        DateTime Timestamp,
        double Frequency,
        byte Modulation,
        byte Encryption,
        uint TransmitterUnitId,
        ulong PacketId,
        string TransmitterGuid,
        int SampleRate,
        int ChannelCount,
        int Coalition,
        byte[] AudioPayload
    )
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public bool TryWriteMetadata(BinaryWriter writer)
        {
            try
            {
                writer.Write(Timestamp.Ticks);
                writer.Write(Frequency);
                writer.Write(Modulation);
                writer.Write(Encryption);
                writer.Write(TransmitterUnitId);
                writer.Write(PacketId);

                var guidBytes = Encoding.ASCII.GetBytes(TransmitterGuid ?? string.Empty);
                Array.Resize(ref guidBytes, 22);
                writer.Write(guidBytes);

                writer.Write(AudioPayload?.Length ?? 0);
                if (AudioPayload != null && AudioPayload.Length > 0)
                    writer.Write(AudioPayload);

                writer.Write(Coalition);
                //Logger.Debug($"AudioPacketMetadata written: Freq={Frequency}, TxGuid={TransmitterGuid}, Size={AudioPayload?.Length ?? 0}");
                return true;
            }
            catch (Exception ex)
            {
                //Logger.Error(ex, "Error during AudioPacketMetadata serialization.");
                return false;
            }
        }

        public static bool TryReadMetadata(BinaryReader reader, out AudioPacketMetadata? metadata)
        {
            metadata = null;
            try
            {
                long ticks = reader.ReadInt64();
                double frequency = reader.ReadDouble();
                byte modulation = reader.ReadByte();
                byte encryption = reader.ReadByte();
                uint transmitterUnitId = reader.ReadUInt32();
                ulong packetId = reader.ReadUInt64();
                byte[] guidBytes = reader.ReadBytes(22);
                string transmitterGuid = Encoding.ASCII.GetString(guidBytes).TrimEnd('\0');

                int audioLength = reader.ReadInt32();
                byte[] audioPayload = audioLength > 0 ? reader.ReadBytes(audioLength) : Array.Empty<byte>();

                int coalition = reader.ReadInt32();

                metadata = new AudioPacketMetadata(
                    new DateTime(ticks, DateTimeKind.Utc),
                    frequency,
                    modulation,
                    encryption,
                    transmitterUnitId,
                    packetId,
                    transmitterGuid,
                    48000, // SampleRate (default or fetch as needed)
                    1,     // ChannelCount (default or fetch as needed)
                    coalition,
                    audioPayload
                );
                //Logger.Debug($"AudioPacketMetadata read: Freq={frequency}, TxGuid={transmitterGuid}, Size={audioPayload.Length}");
                return true;
            }
            catch (EndOfStreamException ex)
            {
                //Logger.Warn(ex, "Reached end of stream during AudioPacketMetadata deserialization.");
                return false;
            }
            catch (Exception ex)
            {
               // Logger.Error(ex, "Error during AudioPacketMetadata deserialization.");
                return false;
            }
        }
    }
}