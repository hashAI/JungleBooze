using System;
using System.IO;
using System.Text;

namespace JungleBooze.Core
{
    /// <summary>
    /// Binary replay file (ADR 0006 and its 2026-10-09 amendment, format 3). Little-endian layout:
    /// magic "JBRP" (4 bytes), int32 format version, uint64 seed, uint64 config hash, string build version
    /// (length-prefixed UTF-8); setup: uint8 present (0/1) and, if present, uint8 flags (bit 0 first expedition),
    /// int32 owned, int32 pending showcase, float32 skill, float32 forced speed, int32 discovered count, strings;
    /// int32 marker count, per marker int64 tick, uint8 kind; int32 frame count, per frame int64 tick,
    /// uint8 commands, int16 lateral mm. Reading a different format version throws
    /// <see cref="NotSupportedException"/>: replays are never migrated. Allocates; call at run end or load time only.
    /// </summary>
    public static class InputRecordingSerializer
    {
        private static readonly byte[] Magic = { (byte)'J', (byte)'B', (byte)'R', (byte)'P' };

        public static void Write(InputRecording recording, Stream stream)
        {
            if (recording == null)
            {
                throw new ArgumentNullException(nameof(recording));
            }

            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(InputRecording.CurrentFormatVersion);
                writer.Write(recording.Seed);
                writer.Write(recording.ConfigHash);
                writer.Write(recording.BuildVersion);
                ReplaySetup setup = recording.Setup;
                writer.Write((byte)(setup != null ? 1 : 0));
                if (setup != null)
                {
                    writer.Write((byte)(setup.FirstExpedition ? 1 : 0));
                    writer.Write(setup.Owned);
                    writer.Write(setup.PendingShowcase);
                    writer.Write(setup.Skill);
                    writer.Write(setup.ForcedSpeed);
                    string[] discovered = setup.Discovered ?? Array.Empty<string>();
                    writer.Write(discovered.Length);
                    for (int i = 0; i < discovered.Length; i++)
                    {
                        writer.Write(discovered[i] ?? string.Empty);
                    }
                }

                writer.Write(recording.MarkerCount);
                for (int i = 0; i < recording.MarkerCount; i++)
                {
                    ReplayMarker marker = recording.GetMarker(i);
                    writer.Write(marker.Tick);
                    writer.Write((byte)marker.Kind);
                }

                writer.Write(recording.Count);
                for (int i = 0; i < recording.Count; i++)
                {
                    RecordedInput input = recording[i];
                    writer.Write(input.Tick);
                    writer.Write((byte)input.Frame.Commands);
                    writer.Write(input.Frame.LateralDeltaMm);
                }
            }
        }

        public static InputRecording Read(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                byte[] magic = reader.ReadBytes(Magic.Length);
                for (int i = 0; i < Magic.Length; i++)
                {
                    if (magic.Length != Magic.Length || magic[i] != Magic[i])
                    {
                        throw new InvalidDataException("Not a replay file (bad magic).");
                    }
                }

                int version = reader.ReadInt32();
                if (version != InputRecording.CurrentFormatVersion)
                {
                    throw new NotSupportedException(
                        "Replay format " + version + " is not supported (this build reads format " +
                        InputRecording.CurrentFormatVersion + ").");
                }

                ulong seed = reader.ReadUInt64();
                ulong configHash = reader.ReadUInt64();
                string build = reader.ReadString();
                ReplaySetup setup = null;
                if (reader.ReadByte() != 0)
                {
                    setup = new ReplaySetup
                    {
                        FirstExpedition = (reader.ReadByte() & 1) != 0,
                        Owned = reader.ReadInt32(),
                        PendingShowcase = reader.ReadInt32(),
                        Skill = reader.ReadSingle(),
                        ForcedSpeed = reader.ReadSingle(),
                    };
                    int discovered = reader.ReadInt32();
                    if (discovered < 0 || discovered > 4096)
                    {
                        throw new InvalidDataException("Bad discovered count.");
                    }

                    setup.Discovered = new string[discovered];
                    for (int i = 0; i < discovered; i++)
                    {
                        setup.Discovered[i] = reader.ReadString();
                    }
                }

                int markers = reader.ReadInt32();
                if (markers < 0)
                {
                    throw new InvalidDataException("Negative marker count.");
                }

                var recording = new InputRecording(seed, configHash, build, 16) { Setup = setup };
                for (int i = 0; i < markers; i++)
                {
                    long tick = reader.ReadInt64();
                    recording.AddMarker(tick, (ReplayMarkerKind)reader.ReadByte());
                }

                int count = reader.ReadInt32();
                if (count < 0)
                {
                    throw new InvalidDataException("Negative frame count.");
                }

                for (int i = 0; i < count; i++)
                {
                    long tick = reader.ReadInt64();
                    var commands = (InputCommand)reader.ReadByte();
                    short lateral = reader.ReadInt16();
                    recording.Add(tick, new InputFrame(commands, lateral));
                }

                return recording;
            }
        }
    }
}
