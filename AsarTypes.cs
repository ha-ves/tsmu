/* 
 * Copyright (C) 2025 Tekat, ha-ves
 * 
 * This program is licensed under the GNU Affero General Public License v3 or later.
 * See <https://www.gnu.org/licenses/>.
*/
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using TyranoScriptMemoryUnlocker.Asar;

namespace TyranoScriptMemoryUnlocker.Asar.Data
{
    #region from https://github.com/electron/asar#format
    /*
     * ## Format
     * 
     * Asar uses [Pickle][pickle] to safely serialize binary value to file.
     *
     * The format of asar is very flat:
     *
     * | UInt32: header_size | String: header | Bytes: file1 | ... | Bytes: file42 |
     *
     * The `header_size` and `header` are serialized with [Pickle][pickle] class, and
     * `header_size`'s [Pickle][pickle] object is 8 bytes.
     *
     * The `header` is a JSON string, and the `header_size` is the size of `header`'s
     * `Pickle` object.
     *
     * Structure of `header` is something like this:
     *
     * {
     *    "files": {
     *       "tmp": {
     *          "files": {}
     *       },
     *       "usr" : {
     *          "files": {
     *            "bin": {
     *              "files": {
     *                "ls": {
     *                  "offset": "0",
     *                  "size": 100,
     *                  "executable": true,
     *                  "integrity": {
     *                    "algorithm": "SHA256",
     *                    "hash": "...",
     *                    "blockSize": 1024,
     *                    "blocks": ["...", "..."]
     *                  }
     *                },
     *                "cd": {
     *                  "offset": "100",
     *                  "size": 100,
     *                  "executable": true,
     *                  "integrity": {
     *                    "algorithm": "SHA256",
     *                    "hash": "...",
     *                    "blockSize": 1024,
     *                    "blocks": ["...", "..."]
     *                  }
     *                }
     *              }
     *            }
     *          }
     *       },
     *       "etc": {
     *          "files": {
     *            "hosts": {
     *              "offset": "200",
     *              "size": 32,
     *              "integrity": {
     *                 "algorithm": "SHA256",
     *                 "hash": "...",
     *                 "blockSize": 1024,
     *                 "blocks": ["...", "..."]
     *               }
     *            }
     *          }
     *       }
     *    }
     * }
     *
     * `offset` and `size` records the information to read the file from archive, the
     * `offset` starts from 0 so you have to manually add the size of `header_size` and
     * `header` to the `offset` to get the real offset of the file.
     *
     * `offset` is a UINT64 number represented in string, because there is no way to
     * precisely represent UINT64 in JavaScript `Number`. `size` is a JavaScript
     * `Number` that is no larger than `Number.MAX_SAFE_INTEGER`, which has a value of
     * `9007199254740991` and is about 8PB in size. We didn't store `size` in UINT64
     * because file size in Node.js is represented as `Number` and it is not safe to
     * convert `Number` to UINT64.
     *
     * `integrity` is an object consisting of a few keys:
     * * A hashing `algorithm`, currently only `SHA256` is supported.
     * * A hex encoded `hash` value representing the hash of the entire file.
     * * An array of hex encoded hashes for the `blocks` of the file.  i.e. for a blockSize of 4KB this array contains the hash of every block if you split the file into N 4KB blocks.
     * * A integer value `blockSize` representing the size in bytes of each block in the `blocks` hashes above
     *
     * [pickle]: https://chromium.googlesource.com/chromium/src/+/main/base/pickle.h
     */
    #endregion

    internal class AsarConstants
    {
        internal const string AsarNodeKey = "files";
    }

    [JsonConverter(typeof(AsarHeaderJsonConverter))]
    public class AsarHeader : IDisposable
    {
        [JsonPropertyName(AsarConstants.AsarNodeKey)]
        public ConcurrentDictionary<string, AsarNode>? Nodes { get; set; }

        [JsonConverter(typeof(AsarNodeJsonConverter))]
        public abstract class AsarNode
        {
            // Base class for file and folder nodes

            [JsonIgnore]
            public string? Name { get; set; }
        }

        public class AsarDirectoryNode : AsarNode
        {
            [JsonPropertyName(AsarConstants.AsarNodeKey)]
            public ConcurrentDictionary<string, AsarNode>? Files { get; set; }
        }

        public class AsarFileNode : AsarNode
        {
            [JsonPropertyName("offset")]
            [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
            public long? Offset { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }
        }

        public class AsarHeaderJsonConverter : JsonConverter<AsarHeader>
        {
            public override AsarHeader? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var elem = doc.RootElement.GetProperty(AsarConstants.AsarNodeKey);

                var d = elem.Deserialize<ConcurrentDictionary<string, AsarNode>?>(options);

                Parallel.ForEach(elem.EnumerateObject().Select(prop => prop.Name), name =>
                {
                    // Ensure each node has a Name property set
                    if (d == null || !d.TryGetValue(name, out AsarNode? value))
                        return;
                    value.Name = name;
                });

                return new AsarHeader
                {
                    Nodes = d
                };
            }
            public override void Write(Utf8JsonWriter writer, AsarHeader value, JsonSerializerOptions options)
            {
                throw new NotImplementedException("Serializing is not implemented yet.");
            }
        }

        public class AsarNodeJsonConverter : JsonConverter<AsarNode>
        {
            public override AsarNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                using var doc = JsonDocument.ParseValue(ref reader);
                var nodeElem = doc.RootElement;

                if (nodeElem.TryGetProperty(AsarConstants.AsarNodeKey, out var filesProp) && filesProp.ValueKind == JsonValueKind.Object)
                {
                    // It's a folder node
                    var elem = doc.RootElement.GetProperty(AsarConstants.AsarNodeKey);
                    var d = elem.Deserialize<ConcurrentDictionary<string, AsarNode>?>(options);

                    Parallel.ForEach(elem.EnumerateObject().Select(prop => prop.Name), name =>
                    {
                        // Ensure each node has a Name property set
                        if (d == null || !d.TryGetValue(name, out AsarNode? value))
                            return;
                        value.Name = name;
                    });

                    return new AsarDirectoryNode
                    {
                        Files = d
                    };
                }
                else
                {
                    // It's a file node
                    return nodeElem.Deserialize<AsarFileNode>(options);
                }
            }

            public override void Write(Utf8JsonWriter writer, AsarNode value, JsonSerializerOptions options)
            {
                throw new NotImplementedException("Serializing is not implemented yet.");
            }
        }

        #region Dispose pattern
        private bool disposedValue;

        [JsonIgnore]
        public bool IsDisposed => disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Nodes != null)
                    {
                        foreach (var node in Nodes.Values)
                        {
                            if (node is AsarDirectoryNode dir && dir.Files != null)
                            {
                                dir.Files.Clear();
                            }
                        }
                        Nodes.Clear();
                    }
                }
                disposedValue = true;
            }
        }
        ~AsarHeader()
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
}