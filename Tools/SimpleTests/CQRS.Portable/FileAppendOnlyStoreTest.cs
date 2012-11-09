﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using Lokad.Cqrs;
using Lokad.Cqrs.TapeStorage;
using NUnit.Framework;

namespace Sample.CQRS.Portable
{
    public class FileAppendOnlyStoreTest
    {
        private readonly string _storePath = Path.Combine(Path.GetTempPath(), "Lokad-CQRS");
        private const int DataFileCount = 10;
        private const int FileMessagesCount = 5;


        void CreateCacheFiles()
        {
            const string msg = "test messages";
            Directory.CreateDirectory(_storePath);
            for (int index = 0; index < DataFileCount; index++)
            {
                using (var stream = new FileStream(Path.Combine(_storePath, index + ".dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    for (int i = 0; i < FileMessagesCount; i++)
                    {
                        StorageFramesEvil.WriteFrame("test-key" + index, i, Encoding.UTF8.GetBytes(msg + i), stream);
                    }
                }
            }
        }

        [Test]
        public void load_cache()
        {
            CreateCacheFiles();
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.LoadCaches();

                for (int j = 0; j < DataFileCount; j++)
                {
                    var key = "test-key" + j;
                    var data = store.ReadRecords(key, -1, Int32.MaxValue);

                    int i = 0;
                    foreach (var dataWithKey in data)
                    {
                        Assert.AreEqual("test messages" + i, Encoding.UTF8.GetString(dataWithKey.Data));
                        i++;
                    }
                    Assert.AreEqual(FileMessagesCount, i);
                }
            }
        }

        [Test]
        public void load_cache_when_exist_empty_file()
        {
            var currentPath = Path.Combine(_storePath, "EmptyCache");
            Directory.CreateDirectory(currentPath);
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(currentPath)))
            {
                //write frame
                using (var stream = new FileStream(Path.Combine(currentPath, "0.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    StorageFramesEvil.WriteFrame("test-key", 0, Encoding.UTF8.GetBytes("test message"), stream);

                //create empty file
                using (var sw = new StreamWriter(Path.Combine(currentPath, "1.dat")))
                    sw.Write("");

                store.LoadCaches();
                var data = store.ReadRecords(0, Int32.MaxValue).ToArray();


                Assert.AreEqual(1, data.Length);
                Assert.AreEqual("test-key", data[0].Key);
                Assert.AreEqual(0, data[0].StreamVersion);
                Assert.AreEqual("test message", Encoding.UTF8.GetString(data[0].Data));
                Assert.IsFalse(File.Exists(Path.Combine(currentPath, "1.dat")));
            }
        }

        [Test]
        public void load_cache_when_incorrect_data_file()
        {
            var currentPath = Path.Combine(_storePath, "EmptyCache");
            Directory.CreateDirectory(currentPath);
            var store = new FileAppendOnlyStore(new DirectoryInfo(currentPath));

            //write frame
            using (var stream = new FileStream(Path.Combine(currentPath, "0.dat"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                StorageFramesEvil.WriteFrame("test-key", 0, Encoding.UTF8.GetBytes("test message"), stream);

            //write incorrect frame
            using (var sw = new StreamWriter(Path.Combine(currentPath, "1.dat")))
                sw.Write("incorrect frame data");

            store.LoadCaches();
            var data = store.ReadRecords(0, Int32.MaxValue).ToArray();


            Assert.AreEqual(1, data.Length);
            Assert.AreEqual("test-key", data[0].Key);
            Assert.AreEqual(0, data[0].StreamVersion);
            Assert.AreEqual("test message", Encoding.UTF8.GetString(data[0].Data));
            Assert.IsTrue(File.Exists(Path.Combine(currentPath, "1.dat")));
        }

        [Test]
        public void append_data()
        {
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();
                const int messagesCount = 3;
                for (int i = 0; i < messagesCount; i++)
                {
                    store.Append("stream1", Encoding.UTF8.GetBytes("test message" + i));
                }

                var data = store.ReadRecords("stream1", currentVersion, Int32.MaxValue).ToArray();

                for (int i = 0; i < messagesCount; i++)
                {
                    Assert.AreEqual("test message" + i, Encoding.UTF8.GetString(data[i].Data));
                }

                Assert.AreEqual(messagesCount, data.Length);
            }
        }

        [Test]
        public void append_data_when_set_version_where_does_not_correspond_real_version()
        {
            var key = Guid.NewGuid().ToString();

            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                store.Append(key, Encoding.UTF8.GetBytes("test message1"), 100);

                var data = store.ReadRecords(key, -1, 2).ToArray();
                CollectionAssert.IsEmpty(data);
            }

        }

        [Test]
        public void get_current_version()
        {
            using (var store = new FileAppendOnlyStore(new DirectoryInfo(_storePath)))
            {
                store.Initialize();
                var currentVersion = store.GetCurrentVersion();
                store.Append("versiontest", Encoding.UTF8.GetBytes("test message1"));
                store.Append("versiontest", Encoding.UTF8.GetBytes("test message2"));
                store.Append("versiontest", Encoding.UTF8.GetBytes("test message3"));

                Assert.AreEqual(currentVersion + 3, store.GetCurrentVersion());
            }
        }
    }
}