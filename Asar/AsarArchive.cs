/* 
 * Copyright (C) 2025 Tekat, ha-ves
 * 
 * This program is licensed under the GNU Affero General Public License v3 or later.
 * See <https://www.gnu.org/licenses/>.
*/
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TyranoScriptMemoryUnlocker.Asar.Data;

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0130 // Namespace for AsarArchive and related classes
namespace TyranoScriptMemoryUnlocker.Asar
{
    public class AsarArchive(FileStream stream) : IDisposable
    {
        private bool disposedValue = false;
        public bool IsDisposed => disposedValue;

        internal long _headerSize;
        internal AsarHeader? _header;

        private AsarDirectoryEntry _files = new();

        public AsarDirectoryEntry Files
        {
            get => _files;
            set
            {
                _header ??= new AsarHeader
                {
                    Nodes = GetAsarNodes(value)
                };

                _files = value;
                _files._topArchiveStream = stream;
            }
        }

        private static ConcurrentDictionary<string, AsarHeader.AsarNode>? GetAsarNodes(AsarDirectoryEntry? dir)
        {
            var nodekvp = dir?._files?.Select(kvp =>
            {
                (var name, var entry) = (kvp.Key, kvp.Value);

                return new KeyValuePair<string, AsarHeader.AsarNode>(
                    name, entry.AsarMeta ?? throw new InvalidOperationException($"Entry must have {nameof(AsarArchiveEntry.AsarMeta)} set."));
            });

            return nodekvp is not null ? new ConcurrentDictionary<string, AsarHeader.AsarNode>(nodekvp) : null;
        }

        public FileStream ReadStream => stream ?? throw new ArgumentNullException(nameof(stream));

        #region Dispose pattern
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _header?.Dispose();
                    _files?.Dispose();
                    ReadStream?.Dispose();
                }
                disposedValue = true;
            }
        }

        ~AsarArchive()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class AsarDirectoryEntry : AsarArchiveEntry
    {
        internal ConcurrentDictionary<string, AsarArchiveEntry>? _files;

        public IEnumerable<AsarFileEntry>? EnumerateFiles()
            => _files?.Values.Where(entry => entry is AsarFileEntry).Cast<AsarFileEntry>();

        public IEnumerable<AsarDirectoryEntry>? EnumerateDirectories()
            => _files?.Values.Where(entry => entry is AsarDirectoryEntry).Cast<AsarDirectoryEntry>();

        public AsarArchiveEntry this[string name]
        {
            get => (TryGetEntry(name, out var entry) ? entry : null) ?? throw new KeyNotFoundException($"Can't find [{name}] in archive.");
        }

        public bool TryGetEntry(string path, out AsarArchiveEntry? entry)
        {
            var goodPath = System.IO.Path.Join(path);

            var firstsplit = goodPath.IndexOfAny(['/', '\\']);

            if (firstsplit < 0)
            {
                // No sub-path, just return the file if it exists
                if (_files?.TryGetValue(goodPath, out var asarFile) ?? false && asarFile is AsarFileEntry)
                {
                    entry = asarFile;
                    return true;
                }
                entry = null;
                return false;
            }

            var nextdir = goodPath[..firstsplit];
            var nextpath = goodPath[(firstsplit + 1)..];

            AsarArchiveEntry? entryt = null;

            var ok = (_files?[nextdir] as AsarDirectoryEntry)?.TryGetEntry(nextpath, out entryt);

            entry = entryt;

            return ok ?? false;
        }

        #region Dispose pattern
        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    if (_files != null)
                    {
                        foreach (var entry in _files.Values)
                        {
                            entry.Dispose();
                        }
                        _files.Clear();
                    }
                }
                base.Dispose(disposing);
            }
        }
        ~AsarDirectoryEntry()
        {
            Dispose(disposing: false);
        }
        #endregion
    }

    public class AsarFileEntry : AsarArchiveEntry
    {
        internal AsarFileEntry()
        {
        }

        public AsarFileReadStream ReadAsFileStream(bool async = false)
        {
            if (ArchiveStream is null)
            {
                throw new InvalidOperationException($"Archive is not initialized. Ensure the {nameof(AsarArchiveEntry)} is part of an {nameof(AsarArchive)}.");
            }

            var meta = (AsarMeta as AsarHeader.AsarFileNode);
            var offset = meta?.Offset
                ?? throw new InvalidOperationException($"{nameof(AsarHeader.AsarFileNode.Offset)} is not set. Ensure the entry has valid metadata.");

            offset += ArchiveStream.Position;

            return new AsarFileReadStream(ArchiveStream, offset, meta.Size, async);
        }

        #region Dispose pattern
        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    // Dispose file info if needed
                }
                base.Dispose(disposing);
            }
        }
        ~AsarFileEntry()
        {
            Dispose(disposing: false);
        }
        #endregion
    }

    public abstract class AsarArchiveEntry : IDisposable
    {
        private bool disposedValue;

        public bool IsDisposed => disposedValue;

        public bool IsArchived { get; internal set; }

        public string? ArchivePath => System.IO.Path.Join(Parent?.ArchivePath, Name);

        public AsarDirectoryEntry? Parent { get; internal set; }

        internal AsarHeader.AsarNode? AsarMeta { get; set; }

        public string? Name { get; internal set; }

        internal FileStream? _topArchiveStream;
        internal FileStream? ArchiveStream => _topArchiveStream ?? Parent?.ArchiveStream;

        #region Dispose pattern
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)

                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        ~AsarArchiveEntry()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

    public class AsarFileReadStream : FileStream
    {
        private readonly long _archiveOffset;
        private readonly long _length;

        public override long Position
        {
            get => base.Position - _archiveOffset;
            set
            {
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Position must be within the bounds of the file entry.");
                }
                base.Position = value + _archiveOffset;
            }
        }

        public override long Length => _length;

        public AsarFileReadStream(FileStream archiveStream, long offset, long size, bool useAsync)
            : base(archiveStream.SafeFileHandle, FileAccess.Read, bufferSize: 0, isAsync: useAsync)
        {
            _archiveOffset = offset;
            _length = size;
            base.Position = offset;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long available = _length - Position;
            if (available <= 0)
            {
                return 0;
            }
            if (count > available)
            {
                count = (int)available;
            }
            return base.Read(buffer, offset, count);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            long available = _length - Position;
            if (available <= 0)
            {
                return 0;
            }
            int toRead = (int)Math.Min(buffer.Length, available);
            return await base.ReadAsync(buffer[..toRead], cancellationToken).ConfigureAwait(false);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosRelative = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException("Invalid SeekOrigin", nameof(origin)),
            };
            if (newPosRelative < 0 || newPosRelative > _length)
            {
                throw new IOException("Attempted to seek outside the bounds of the file entry.");
            }
            base.Seek(newPosRelative + _archiveOffset, SeekOrigin.Begin);
            return newPosRelative;
        }

        public override int ReadByte()
        {
            if (Position >= _length)
            {
                return -1;
            }
            return base.ReadByte();
        }
    }

    public static class AsarFile
    {
        private static readonly JsonSerializerOptions asarParseOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = AsarJsonContext.Default
        };

        public static AsarArchive Open(string filePath)
        {
            var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 0, true);
            return Open(stream);
        }

        public static AsarArchive Open(FileStream stream)
        {
            // Read the ASAR header and files here
            var header = ParseHeader(stream);
            return new AsarArchive(stream)
            {
                _headerSize = stream.Position + 1,
                _header = header,
                Files = PopulateFiles(header.Nodes),
            };
        }

        private static AsarDirectoryEntry PopulateFiles(
            ConcurrentDictionary<string, AsarHeader.AsarNode>? headerNodes)
        {
            var dirfiles = new AsarDirectoryEntry()
            {
                _files = new ConcurrentDictionary<string, AsarArchiveEntry>(),
                IsArchived = true,
            };

            if (headerNodes is not null)
            {
                Parallel.ForEach(headerNodes, kvp =>
                {
                    (var name, var node) = (kvp.Key, kvp.Value);

                    if (node.GetType() == typeof(AsarHeader.AsarDirectoryNode))
                    {
                        var dirEntry = PopulateFiles(((AsarHeader.AsarDirectoryNode)node).Files)!;
                        dirEntry.Name = name;
                        dirEntry.AsarMeta = node as AsarHeader.AsarDirectoryNode;
                        dirEntry.Parent = dirfiles;

                        dirfiles._files[name] = dirEntry;
                    }
                    else
                    {
                        dirfiles._files[name] = new AsarFileEntry()
                        {
                            AsarMeta = node as AsarHeader.AsarFileNode,
                            Parent = dirfiles,
                            IsArchived = true,
                            Name = name,
                        };
                    }
                });
            }

            return dirfiles;
        }

        [SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", 
            Justification = Suppressions.JsonTrimmingJustification)]
        private static AsarHeader ParseHeader(Stream stream)
        {
            using var reader = new PickleReader(stream, Encoding.Default, leaveOpen: true);

            var headerPklSize = reader.ReadObject<uint>();

            var headerpkl = reader.ReadPickle();
            var headerSpan = headerpkl.GetPayloadAsSpan<byte>();

            var headerStr = Encoding.Default.GetString(headerSpan);

            var header = JsonSerializer.Deserialize<AsarHeader>(headerStr, asarParseOptions)
                ?? throw new InvalidDataException("Failed to parse ASAR header.");

            return header;
        }
    }

    public class PickleReader(Stream input, Encoding encoding, bool leaveOpen) 
        : BinaryReader(input, encoding, leaveOpen)
    {
        public PickleObject ReadPickle()
        {
            // based on header size = 4 bytes (uint32)
            // but the source code said it can be customized
            var size = ReadUInt32();
            return new PickleObject
            {
                Payload = ReadBytes((int)size)
            };
        }

        public T ReadObject<T>() where T : struct => ReadPickle().GetPayloadObject<T>();
    }

    public class PickleObject(uint headerSize = sizeof(uint))
    {
        public uint HeaderSize { get; set; } = headerSize;

        public Memory<byte>? Payload { get; set; }

        public T GetPayloadObject<T>() where T : struct
        {
            if (Payload is null)
            {
                throw new InvalidOperationException($"Data is null. Ensure the {nameof(PickleObject)} has been properly initialized.");
            }
            else if (Payload.Value.Length != Unsafe.SizeOf<T>())
            {
                throw new InvalidDataException($"Expected size {Unsafe.SizeOf<T>()}, but got {Payload.Value.Length} bytes.");
            }

            return MemoryMarshal.Read<T>(Payload.Value.Span);
        }

        public Span<T> GetPayloadAsSpan<T>() where T : struct
        {
            if (Payload is null)
            {
                throw new InvalidOperationException($"Data is null. Ensure the {nameof(PickleObject)} has been properly initialized.");
            }

            var dataSize = BitConverter.ToInt32(Payload.Value[..sizeof(int)].Span);
            var arrayPayload = Payload.Value.Slice(sizeof(int), dataSize);

            if (arrayPayload.Length % Marshal.SizeOf<T>() > 0)
            {
                throw new InvalidDataException($"Data length {Payload.Value.Length} is not a multiple of element size {Marshal.SizeOf<T>()}.");
            }

            return MemoryMarshal.Cast<byte, T>(arrayPayload.Span);
        }
    }

    internal static class Suppressions
    {
        internal const string JsonTrimmingJustification = "The Json parsing already using appropriate option.";
    }
}