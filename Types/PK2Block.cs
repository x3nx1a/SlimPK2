﻿#region Usings

using SlimPK2.IO;
using SlimPK2.IO.Stream;
using SlimPK2.Security;
using System.Linq;

#endregion Usings

namespace SlimPK2.Types
{
    public class PK2Block
    {
        #region Properties

        /// <summary>
        /// Gets or sets the entries.
        /// </summary>
        /// <value>
        /// The entries.
        /// </value>
        public PK2Entry[] Entries { get; set; }

        /// <summary>
        /// Gets or sets the block offset.
        /// </summary>
        /// <value>
        /// The block offset.
        /// </value>
        public ulong Offset { get; set; }

        /// <summary>
        /// Gets a value indicating whether this instance has blocks.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has blocks; otherwise, <c>false</c>.
        /// </value>
        public bool HasBlocks => Entries[19].NextChain > 0;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="PK2Block"/> class.
        /// </summary>
        public PK2Block() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PK2Block" /> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The block offset.</param>
        public PK2Block(byte[] buffer, ulong offset)
        {
            Entries = new PK2Entry[20];
            Offset = offset;

            using (var streamWorker = new StreamWorker(buffer, StreamOperation.Read))
            {
                for (var i = 0; i < 20; i++)
                {
                    var entryBuffer = streamWorker.ReadByteArray(128);

                    if (BlowfishUtilities.GetBlowfish() != null)
                        entryBuffer = BlowfishUtilities.GetBlowfish().Decode(entryBuffer);

                    Entries[i] = new PK2Entry(entryBuffer, this, (byte)i);
                }
            }
        }

        /// <summary>
        /// Gets a collection of all blocks that belong to this block.
        /// </summary>
        /// <returns></returns>
        public PK2BlockCollection GetCollection()
        {
            var result = new PK2BlockCollection();

            result.Blocks.Add(this);

            var block = GetNextBlock();

            while (block.HasBlocks)
            {
                result.Blocks.Add(block);
                block = block.GetNextBlock();
            }

            return result;
        }

        /// <summary>
        /// Gets the first empty entry that can be used to write new data to it.
        /// </summary>
        /// <returns></returns>
        internal PK2Entry GetFirstEmptyEntry()
        {
            var collection = GetCollection();

            return collection.GetEntries().FirstOrDefault(entry => entry.Type == PK2EntryType.Empty);
        }

        /// <summary>
        /// A helper method that returns the last block of this chain
        /// </summary>
        /// <returns></returns>
        public PK2Block GetLastBlock()
        {
            var collection = GetCollection();
            return collection.Blocks[collection.Blocks.Count - 1];
        }

        /// <summary>
        /// Gets the next block.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="PK2NotLoadedException"></exception>
        public PK2Block GetNextBlock()
        {
            if (FileAdapter.GetInstance() == null)
                throw new PK2NotLoadedException();

            return HasBlocks ? new PK2Block(FileAdapter.GetInstance().ReadData((long)Entries[19].NextChain, 2560), Entries[19].NextChain) : this;
        }

        /// <summary>
        /// To the byte array.
        /// </summary>
        /// <returns></returns>
        public byte[] ToByteArray()
        {
            var buffer = new byte[2560];
            using (var stream = new StreamWorker(buffer, StreamOperation.Write))
            {
                for (var i = 0; i < 20; i++)
                {
                    stream.WriteByteArray(Entries[i].ToByteArray());
                }
            }
            return buffer;
        }

        /// <summary>
        /// Saves the block back to the PK2 archive.
        /// </summary>
        /// <exception cref="PK2NotLoadedException"></exception>
        public void Save()
        {
            if (FileAdapter.GetInstance() == null)
                throw new PK2NotLoadedException();

            FileAdapter.GetInstance().WriteData(ToByteArray(), (long)Offset);
        }

        /// <summary>
        /// Creates a new block within the PK2.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <returns></returns>
        /// <exception cref="PK2NotLoadedException"></exception>
        public static PK2Block Create(PK2Entry[] entries = null)
        {
            if (FileAdapter.GetInstance() == null)
                throw new PK2NotLoadedException();

            var buffer = new byte[2560];

            using (var stream = new StreamWorker(buffer, StreamOperation.Write))
            {
                for (var i = 0; i < 20; i++)
                    stream.WriteByteArray(entries?[i] != null ? entries[i].ToByteArray() : new PK2Entry().ToByteArray());
            }

            return new PK2Block(buffer, (ulong)FileAdapter.GetInstance().AppendData(buffer));
        }

        #endregion Constructor
    }
}