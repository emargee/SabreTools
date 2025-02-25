﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Data.Sqlite;
using NaturalSort;
using Newtonsoft.Json;
using SabreTools.Core;
using SabreTools.DatItems;
using SabreTools.DatItems.Formats;
using SabreTools.IO;
using SabreTools.Logging;

namespace SabreTools.DatFiles
{
    /// <summary>
    /// Item dictionary with statistics, bucketing, and sorting
    /// </summary>
    [JsonObject("items"), XmlRoot("items")]
    public class ItemDictionaryDB : IDictionary<string, ConcurrentList<DatItem>>
    {
        #region Private instance variables

        /// <summary>
        /// Determine the bucketing key for all items
        /// </summary>
        private ItemKey bucketedBy;

        /// <summary>
        /// Determine merging type for all items
        /// </summary>
        private DedupeType mergedBy;

        /// <summary>
        /// Internal database connection for the class
        /// </summary>
        private readonly SqliteConnection dbc = null;

        /// <summary>
        /// Internal item dictionary name
        /// </summary>
        private string itemDictionaryFileName = null;

        /// <summary>
        /// Lock for statistics calculation
        /// </summary>
        private readonly object statsLock = new object();

        /// <summary>
        /// Logging object
        /// </summary>
        private readonly Logger logger;

        #endregion

        #region Publically available fields

        #region Database

        /// <summary>
        /// Item dictionary file name
        /// </summary>
        public string ItemDictionaryFileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(itemDictionaryFileName))
                    itemDictionaryFileName = Path.Combine(PathTool.GetRuntimeDirectory(), $"itemDictionary{Guid.NewGuid()}.sqlite");

                return itemDictionaryFileName;
            }
        }

        /// <summary>
        /// Item dictionary connection string
        /// </summary>
        public string ItemDictionaryConnectionString => $"Data Source={ItemDictionaryFileName};";

        #endregion

        #region Keys

        /// <summary>
        /// Get the keys from the file database
        /// </summary>
        /// <returns>List of the keys</returns>
        [JsonIgnore, XmlIgnore]
        public ICollection<string> Keys
        {
            get
            {
                // If we have no database connection, we can't do anything
                if (dbc == null)
                    return null;

                // Open the database connection
                dbc.Open();

                string query = $"SELECT key FROM keys";
                SqliteCommand slc = new SqliteCommand(query, dbc);
                SqliteDataReader sldr = slc.ExecuteReader();

                List<string> keys = new List<string>();
                if (sldr.HasRows)
                {
                    while (sldr.Read())
                    {
                        keys.Add(sldr.GetString(0));
                    }
                }

                // Dispose of database objects
                slc.Dispose();
                sldr.Dispose();
                dbc.Close();

                return keys;
            }
        }

        /// <summary>
        /// Get the keys in sorted order from the file dictionary
        /// </summary>
        /// <returns>List of the keys in sorted order</returns>
        [JsonIgnore, XmlIgnore]
        public List<string> SortedKeys
        {
            get
            {
                var keys = Keys.ToList();
                keys.Sort(new NaturalComparer());
                return keys;
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Overall item count
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long TotalCount { get; private set; } = 0;

        /// <summary>
        /// Number of Adjuster items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long AdjusterCount { get; private set; } = 0;

        /// <summary>
        /// Number of Analog items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long AnalogCount { get; private set; } = 0;

        /// <summary>
        /// Number of Archive items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long ArchiveCount { get; private set; } = 0;

        /// <summary>
        /// Number of BiosSet items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long BiosSetCount { get; private set; } = 0;

        /// <summary>
        /// Number of Chip items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long ChipCount { get; private set; } = 0;

        /// <summary>
        /// Number of top-level Condition items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long ConditionCount { get; private set; } = 0;

        /// <summary>
        /// Number of Configuration items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long ConfigurationCount { get; private set; } = 0;

        /// <summary>
        /// Number of DataArea items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DataAreaCount { get; private set; } = 0;

        /// <summary>
        /// Number of Device items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DeviceCount { get; private set; } = 0;

        /// <summary>
        /// Number of Device Reference items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DeviceReferenceCount { get; private set; } = 0;

        /// <summary>
        /// Number of DIP Switch items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DipSwitchCount { get; private set; } = 0;

        /// <summary>
        /// Number of Disk items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DiskCount { get; private set; } = 0;

        /// <summary>
        /// Number of DiskArea items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DiskAreaCount { get; private set; } = 0;

        /// <summary>
        /// Number of Display items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DisplayCount { get; private set; } = 0;

        /// <summary>
        /// Number of Driver items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long DriverCount { get; private set; } = 0;

        /// <summary>
        /// Number of Feature items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long FeatureCount { get; private set; } = 0;

        /// <summary>
        /// Number of Info items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long InfoCount { get; private set; } = 0;

        /// <summary>
        /// Number of Input items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long InputCount { get; private set; } = 0;

        /// <summary>
        /// Number of Media items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long MediaCount { get; private set; } = 0;

        /// <summary>
        /// Number of Part items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long PartCount { get; private set; } = 0;

        /// <summary>
        /// Number of PartFeature items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long PartFeatureCount { get; private set; } = 0;

        /// <summary>
        /// Number of Port items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long PortCount { get; private set; } = 0;

        /// <summary>
        /// Number of RamOption items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long RamOptionCount { get; private set; } = 0;

        /// <summary>
        /// Number of Release items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long ReleaseCount { get; private set; } = 0;

        /// <summary>
        /// Number of Rom items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long RomCount { get; private set; } = 0;

        /// <summary>
        /// Number of Sample items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SampleCount { get; private set; } = 0;

        /// <summary>
        /// Number of SharedFeature items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SharedFeatureCount { get; private set; } = 0;

        /// <summary>
        /// Number of Slot items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SlotCount { get; private set; } = 0;

        /// <summary>
        /// Number of SoftwareList items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SoftwareListCount { get; private set; } = 0;

        /// <summary>
        /// Number of Sound items
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SoundCount { get; private set; } = 0;

        /// <summary>
        /// Number of machines
        /// </summary>
        /// <remarks>Special count only used by statistics output</remarks>
        [JsonIgnore, XmlIgnore]
        public long GameCount { get; set; } = 0;

        /// <summary>
        /// Total uncompressed size
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long TotalSize { get; private set; } = 0;

        /// <summary>
        /// Number of items with a CRC hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long CRCCount { get; private set; } = 0;

        /// <summary>
        /// Number of items with an MD5 hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long MD5Count { get; private set; } = 0;

        /// <summary>
        /// Number of items with a SHA-1 hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SHA1Count { get; private set; } = 0;

        /// <summary>
        /// Number of items with a SHA-256 hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SHA256Count { get; private set; } = 0;

        /// <summary>
        /// Number of items with a SHA-384 hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SHA384Count { get; private set; } = 0;

        /// <summary>
        /// Number of items with a SHA-512 hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SHA512Count { get; private set; } = 0;

        /// <summary>
        /// Number of items with a SpamSum fuzzy hash
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long SpamSumCount { get; private set; } = 0;

        /// <summary>
        /// Number of items with the baddump status
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long BaddumpCount { get; private set; } = 0;

        /// <summary>
        /// Number of items with the good status
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long GoodCount { get; private set; } = 0;

        /// <summary>
        /// Number of items with the nodump status
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long NodumpCount { get; private set; } = 0;

        /// <summary>
        /// Number of items with the remove flag
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long RemovedCount { get; private set; } = 0;

        /// <summary>
        /// Number of items with the verified status
        /// </summary>
        [JsonIgnore, XmlIgnore]
        public long VerifiedCount { get; private set; } = 0;

        #endregion

        #endregion

        #region Accessors

        /// <summary>
        /// Passthrough to access the file dictionary
        /// </summary>
        /// <param name="key">Key in the dictionary to reference</param>
        public ConcurrentList<DatItem> this[string key]
        {
            get
            {
                // Explicit lock for some weird corner cases
                lock (key)
                {
                    // Ensure the key exists
                    EnsureKey(key);

                    // If we have no database connection, we can't do anything
                    if (dbc == null)
                        return null;

                    // Open the database connection
                    dbc.Open();

                    string query = $"SELECT item FROM groups WHERE key='{key}'";
                    SqliteCommand slc = new SqliteCommand(query, dbc);
                    SqliteDataReader sldr = slc.ExecuteReader();

                    ConcurrentList<DatItem> items = new ConcurrentList<DatItem>();
                    if (sldr.HasRows)
                    {
                        while (sldr.Read())
                        {
                            string itemString = sldr.GetString(0);
                            DatItem datItem = JsonConvert.DeserializeObject<DatItem>(itemString);
                            items.Add(datItem);
                        }
                    }

                    // Dispose of database objects
                    slc.Dispose();
                    sldr.Dispose();
                    dbc.Close();

                    // Now return the value
                    return items;
                }
            }
            set
            {
                Remove(key);
                if (value == null)
                {
                    // If we have no database connection, we can't do anything
                    if (dbc == null)
                        return;

                    // Open the database connection
                    dbc.Open();

                    // Now remove the value
                    string itemString = JsonConvert.SerializeObject(value);
                    string query = $"DELETE FROM groups WHERE key='{key}'";
                    SqliteCommand slc = new SqliteCommand(query, dbc);
                    slc.ExecuteNonQuery();

                    // Dispose of database objects
                    slc.Dispose();
                    dbc.Close();
                }
                else
                {
                    AddRange(key, value);
                }
            }
        }

        /// <summary>
        /// Add a value to the file dictionary
        /// </summary>
        /// <param name="key">Key in the dictionary to add to</param>
        /// <param name="value">Value to add to the dictionary</param>
        public void Add(string key, DatItem value)
        {
            // Explicit lock for some weird corner cases
            lock (key)
            {
                // Ensure the key exists
                EnsureKey(key);

                // If item is null, don't add it
                if (value == null)
                    return;

                // If we have no database connection, we can't do anything
                if (dbc == null)
                    return;

                // Open the database connection
                dbc.Open();

                // Now add the value
                string itemString = JsonConvert.SerializeObject(value);
                string query = $"INSERT INTO groups (key, item) VALUES ('{key}', '{itemString}')";
                SqliteCommand slc = new SqliteCommand(query, dbc);
                slc.ExecuteNonQuery();

                // Dispose of database objects
                slc.Dispose();
                dbc.Close();

                // Now update the statistics
                AddItemStatistics(value);
            }
        }

        /// <summary>
        /// Add a range of values to the file dictionary
        /// </summary>
        /// <param name="key">Key in the dictionary to add to</param>
        /// <param name="value">Value to add to the dictionary</param>
        public void Add(string key, ConcurrentList<DatItem> value)
        {
            AddRange(key, value);
        }

        /// <summary>
        /// Add to the statistics given a DatItem
        /// </summary>
        /// <param name="item">Item to add info from</param>
        public void AddItemStatistics(DatItem item)
        {
            lock (statsLock)
            {
                // No matter what the item is, we increment the count
                TotalCount++;

                // Increment removal count
                if (item.Remove)
                    RemovedCount++;

                // Now we do different things for each item type
                switch (item.ItemType)
                {
                    case ItemType.Adjuster:
                        AdjusterCount++;
                        break;
                    case ItemType.Analog:
                        AnalogCount++;
                        break;
                    case ItemType.Archive:
                        ArchiveCount++;
                        break;
                    case ItemType.BiosSet:
                        BiosSetCount++;
                        break;
                    case ItemType.Chip:
                        ChipCount++;
                        break;
                    case ItemType.Condition:
                        ConditionCount++;
                        break;
                    case ItemType.Configuration:
                        ConfigurationCount++;
                        break;
                    case ItemType.DataArea:
                        DataAreaCount++;
                        break;
                    case ItemType.Device:
                        DeviceCount++;
                        break;
                    case ItemType.DeviceReference:
                        DeviceReferenceCount++;
                        break;
                    case ItemType.DipSwitch:
                        DipSwitchCount++;
                        break;
                    case ItemType.Disk:
                        DiskCount++;
                        if ((item as Disk).ItemStatus != ItemStatus.Nodump)
                        {
                            MD5Count += (string.IsNullOrWhiteSpace((item as Disk).MD5) ? 0 : 1);
                            SHA1Count += (string.IsNullOrWhiteSpace((item as Disk).SHA1) ? 0 : 1);
                        }

                        BaddumpCount += ((item as Disk).ItemStatus == ItemStatus.BadDump ? 1 : 0);
                        GoodCount += ((item as Disk).ItemStatus == ItemStatus.Good ? 1 : 0);
                        NodumpCount += ((item as Disk).ItemStatus == ItemStatus.Nodump ? 1 : 0);
                        VerifiedCount += ((item as Disk).ItemStatus == ItemStatus.Verified ? 1 : 0);
                        break;
                    case ItemType.DiskArea:
                        DiskAreaCount++;
                        break;
                    case ItemType.Display:
                        DisplayCount++;
                        break;
                    case ItemType.Driver:
                        DriverCount++;
                        break;
                    case ItemType.Feature:
                        FeatureCount++;
                        break;
                    case ItemType.Info:
                        InfoCount++;
                        break;
                    case ItemType.Input:
                        InputCount++;
                        break;
                    case ItemType.Media:
                        MediaCount++;
                        MD5Count += (string.IsNullOrWhiteSpace((item as Media).MD5) ? 0 : 1);
                        SHA1Count += (string.IsNullOrWhiteSpace((item as Media).SHA1) ? 0 : 1);
                        SHA256Count += (string.IsNullOrWhiteSpace((item as Media).SHA256) ? 0 : 1);
                        SpamSumCount += (string.IsNullOrWhiteSpace((item as Media).SpamSum) ? 0 : 1);
                        break;
                    case ItemType.Part:
                        PartCount++;
                        break;
                    case ItemType.PartFeature:
                        PartFeatureCount++;
                        break;
                    case ItemType.Port:
                        PortCount++;
                        break;
                    case ItemType.RamOption:
                        RamOptionCount++;
                        break;
                    case ItemType.Release:
                        ReleaseCount++;
                        break;
                    case ItemType.Rom:
                        RomCount++;
                        if ((item as Rom).ItemStatus != ItemStatus.Nodump)
                        {
                            TotalSize += (item as Rom).Size ?? 0;
                            CRCCount += (string.IsNullOrWhiteSpace((item as Rom).CRC) ? 0 : 1);
                            MD5Count += (string.IsNullOrWhiteSpace((item as Rom).MD5) ? 0 : 1);
                            SHA1Count += (string.IsNullOrWhiteSpace((item as Rom).SHA1) ? 0 : 1);
                            SHA256Count += (string.IsNullOrWhiteSpace((item as Rom).SHA256) ? 0 : 1);
                            SHA384Count += (string.IsNullOrWhiteSpace((item as Rom).SHA384) ? 0 : 1);
                            SHA512Count += (string.IsNullOrWhiteSpace((item as Rom).SHA512) ? 0 : 1);
                            SpamSumCount += (string.IsNullOrWhiteSpace((item as Rom).SpamSum) ? 0 : 1);
                        }

                        BaddumpCount += ((item as Rom).ItemStatus == ItemStatus.BadDump ? 1 : 0);
                        GoodCount += ((item as Rom).ItemStatus == ItemStatus.Good ? 1 : 0);
                        NodumpCount += ((item as Rom).ItemStatus == ItemStatus.Nodump ? 1 : 0);
                        VerifiedCount += ((item as Rom).ItemStatus == ItemStatus.Verified ? 1 : 0);
                        break;
                    case ItemType.Sample:
                        SampleCount++;
                        break;
                    case ItemType.SharedFeature:
                        SharedFeatureCount++;
                        break;
                    case ItemType.Slot:
                        SlotCount++;
                        break;
                    case ItemType.SoftwareList:
                        SoftwareListCount++;
                        break;
                    case ItemType.Sound:
                        SoundCount++;
                        break;
                }
            }
        }

        /// <summary>
        /// Add a range of values to the file dictionary
        /// </summary>
        /// <param name="key">Key in the dictionary to add to</param>
        /// <param name="value">Value to add to the dictionary</param>
        public void AddRange(string key, ConcurrentList<DatItem> value)
        {
            // Explicit lock for some weird corner cases
            lock (key)
            {
                // If the value is null or empty, just return
                if (value == null || !value.Any())
                    return;

                // Ensure the key exists
                EnsureKey(key);

                // Now add the value
                foreach (DatItem item in value)
                {
                    Add(key, item);
                }

                // Now update the statistics
                foreach (DatItem item in value)
                {
                    AddItemStatistics(item);
                }
            }
        }

        /// <summary>
        /// Add statistics from another DatStats object
        /// </summary>
        /// <param name="stats">DatStats object to add from</param>
        public void AddStatistics(ItemDictionary stats)
        {
            TotalCount += stats.Count;

            ArchiveCount += stats.ArchiveCount;
            BiosSetCount += stats.BiosSetCount;
            ChipCount += stats.ChipCount;
            DiskCount += stats.DiskCount;
            MediaCount += stats.MediaCount;
            ReleaseCount += stats.ReleaseCount;
            RomCount += stats.RomCount;
            SampleCount += stats.SampleCount;

            GameCount += stats.GameCount;

            TotalSize += stats.TotalSize;

            // Individual hash counts
            CRCCount += stats.CRCCount;
            MD5Count += stats.MD5Count;
            SHA1Count += stats.SHA1Count;
            SHA256Count += stats.SHA256Count;
            SHA384Count += stats.SHA384Count;
            SHA512Count += stats.SHA512Count;
            SpamSumCount += stats.SpamSumCount;

            // Individual status counts
            BaddumpCount += stats.BaddumpCount;
            GoodCount += stats.GoodCount;
            NodumpCount += stats.NodumpCount;
            RemovedCount += stats.RemovedCount;
            VerifiedCount += stats.VerifiedCount;
        }

        /// <summary>
        /// Get if the file dictionary contains the key
        /// </summary>
        /// <param name="key">Key in the dictionary to check</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool ContainsKey(string key)
        {
            // If the key is null, we return false since keys can't be null
            if (key == null)
                return false;

            // Explicit lock for some weird corner cases
            lock (key)
            {
                return Keys.Contains(key);
            }
        }

        /// <summary>
        /// Get if the file dictionary contains the key and value
        /// </summary>
        /// <param name="key">Key in the dictionary to check</param>
        /// <param name="value">Value in the dictionary to check</param>
        /// <returns>True if the key exists, false otherwise</returns>
        public bool Contains(string key, DatItem value)
        {
            // If the key is null, we return false since keys can't be null
            if (key == null)
                return false;

            // Explicit lock for some weird corner cases
            lock (key)
            {
                if (!Keys.Contains(key))
                    return false;

                return this[key].Contains(value);
            }
        }

        /// <summary>
        /// Ensure the key exists in the items dictionary
        /// </summary>
        /// <param name="key">Key to ensure</param>
        public void EnsureKey(string key)
        {
            // If the key is missing from the dictionary, add it
            if (Keys.Contains(key))
                return;

            // If we have no database connection, we can't do anything
            if (dbc == null)
                return;

            // Open the database connection
            dbc.Open();

            string query = $"INSERT INTO keys (key) VALUES ('{key}')";
            SqliteCommand slc = new SqliteCommand(query, dbc);
            slc.ExecuteNonQuery();

            // Dispose of database objects
            slc.Dispose();
            dbc.Close();
        }

        /// <summary>
        /// Get a list of filtered items for a given key
        /// </summary>
        /// <param name="key">Key in the dictionary to retrieve</param>
        public ConcurrentList<DatItem> FilteredItems(string key)
        {
            lock (key)
            {
                // Get the list, if possible
                ConcurrentList<DatItem> fi = this[key];
                if (fi == null)
                    return new ConcurrentList<DatItem>();

                // Filter the list
                return fi.Where(i => i != null)
                    .Where(i => !i.Remove)
                    .Where(i => i.Machine?.Name != null)
                    .ToConcurrentList();
            }
        }

        /// <summary>
        /// Remove a key from the file dictionary if it exists
        /// </summary>
        /// <param name="key">Key in the dictionary to remove</param>
        public bool Remove(string key)
        {
            // Explicit lock for some weird corner cases
            lock (key)
            {
                // If the key doesn't exist, return
                if (!ContainsKey(key))
                    return false;

                // Remove the statistics first
                foreach (DatItem item in this[key])
                {
                    RemoveItemStatistics(item);
                }

                // Remove the key from the dictionary
                this[key] = null;
                return true;
            }
        }

        /// <summary>
        /// Remove the first instance of a value from the file dictionary if it exists
        /// </summary>
        /// <param name="key">Key in the dictionary to remove from</param>
        /// <param name="value">Value to remove from the dictionary</param>
        public bool Remove(string key, DatItem value)
        {
            // Explicit lock for some weird corner cases
            lock (key)
            {
                // If the key and value doesn't exist, return
                if (!Contains(key, value))
                    return false;

                // Remove the statistics first
                RemoveItemStatistics(value);

                // If we have no database connection, we can't do anything
                if (dbc == null)
                    return false;

                // Open the database connection
                dbc.Open();

                // Now remove the value
                string itemString = JsonConvert.SerializeObject(value);
                string query = $"DELETE FROM groups WHERE key='{key}' AND item='{itemString}'";
                SqliteCommand slc = new SqliteCommand(query, dbc);
                slc.ExecuteNonQuery();

                // Dispose of database objects
                slc.Dispose();
                dbc.Close();
                return true;
            }
        }

        /// <summary>
        /// Reset a key from the file dictionary if it exists
        /// </summary>
        /// <param name="key">Key in the dictionary to reset</param>
        public bool Reset(string key)
        {
            // If the key doesn't exist, return
            if (!ContainsKey(key))
                return false;

            // Remove the statistics first
            foreach (DatItem item in this[key])
            {
                RemoveItemStatistics(item);
            }

            // Remove the key from the dictionary
            this[key] = null;
            return true;
        }

        /// <summary>
        /// Override the internal ItemKey value
        /// </summary>
        /// <param name="newBucket"></param>
        public void SetBucketedBy(ItemKey newBucket)
        {
            bucketedBy = newBucket;
        }

        /// <summary>
        /// Remove from the statistics given a DatItem
        /// </summary>
        /// <param name="item">Item to remove info for</param>
        public void RemoveItemStatistics(DatItem item)
        {
            // If we have a null item, we can't do anything
            if (item == null)
                return;

            lock (statsLock)
            {
                // No matter what the item is, we decrease the count
                TotalCount--;

                // Decrement removal count
                if (item.Remove)
                    RemovedCount--;

                // Now we do different things for each item type
                switch (item.ItemType)
                {
                    case ItemType.Adjuster:
                        AdjusterCount--;
                        break;
                    case ItemType.Analog:
                        AnalogCount--;
                        break;
                    case ItemType.Archive:
                        ArchiveCount--;
                        break;
                    case ItemType.BiosSet:
                        BiosSetCount--;
                        break;
                    case ItemType.Chip:
                        ChipCount--;
                        break;
                    case ItemType.Condition:
                        ConditionCount--;
                        break;
                    case ItemType.Configuration:
                        ConfigurationCount--;
                        break;
                    case ItemType.DataArea:
                        DataAreaCount--;
                        break;
                    case ItemType.Device:
                        DeviceCount--;
                        break;
                    case ItemType.DeviceReference:
                        DeviceReferenceCount--;
                        break;
                    case ItemType.DipSwitch:
                        DipSwitchCount--;
                        break;
                    case ItemType.Disk:
                        DiskCount--;
                        if ((item as Disk).ItemStatus != ItemStatus.Nodump)
                        {
                            MD5Count -= (string.IsNullOrWhiteSpace((item as Disk).MD5) ? 0 : 1);
                            SHA1Count -= (string.IsNullOrWhiteSpace((item as Disk).SHA1) ? 0 : 1);
                        }

                        BaddumpCount -= ((item as Disk).ItemStatus == ItemStatus.BadDump ? 1 : 0);
                        GoodCount -= ((item as Disk).ItemStatus == ItemStatus.Good ? 1 : 0);
                        NodumpCount -= ((item as Disk).ItemStatus == ItemStatus.Nodump ? 1 : 0);
                        VerifiedCount -= ((item as Disk).ItemStatus == ItemStatus.Verified ? 1 : 0);
                        break;
                    case ItemType.DiskArea:
                        DiskAreaCount--;
                        break;
                    case ItemType.Display:
                        DisplayCount--;
                        break;
                    case ItemType.Driver:
                        DriverCount--;
                        break;
                    case ItemType.Feature:
                        FeatureCount--;
                        break;
                    case ItemType.Info:
                        InfoCount--;
                        break;
                    case ItemType.Input:
                        InputCount--;
                        break;
                    case ItemType.Media:
                        MediaCount--;
                        MD5Count -= (string.IsNullOrWhiteSpace((item as Media).MD5) ? 0 : 1);
                        SHA1Count -= (string.IsNullOrWhiteSpace((item as Media).SHA1) ? 0 : 1);
                        SHA256Count -= (string.IsNullOrWhiteSpace((item as Media).SHA256) ? 0 : 1);
                        break;
                    case ItemType.Part:
                        PartCount--;
                        break;
                    case ItemType.PartFeature:
                        PartFeatureCount--;
                        break;
                    case ItemType.Port:
                        PortCount--;
                        break;
                    case ItemType.RamOption:
                        RamOptionCount--;
                        break;
                    case ItemType.Release:
                        ReleaseCount--;
                        break;
                    case ItemType.Rom:
                        RomCount--;
                        if ((item as Rom).ItemStatus != ItemStatus.Nodump)
                        {
                            TotalSize -= (item as Rom).Size ?? 0;
                            CRCCount -= (string.IsNullOrWhiteSpace((item as Rom).CRC) ? 0 : 1);
                            MD5Count -= (string.IsNullOrWhiteSpace((item as Rom).MD5) ? 0 : 1);
                            SHA1Count -= (string.IsNullOrWhiteSpace((item as Rom).SHA1) ? 0 : 1);
                            SHA256Count -= (string.IsNullOrWhiteSpace((item as Rom).SHA256) ? 0 : 1);
                            SHA384Count -= (string.IsNullOrWhiteSpace((item as Rom).SHA384) ? 0 : 1);
                            SHA512Count -= (string.IsNullOrWhiteSpace((item as Rom).SHA512) ? 0 : 1);
                        }

                        BaddumpCount -= ((item as Rom).ItemStatus == ItemStatus.BadDump ? 1 : 0);
                        GoodCount -= ((item as Rom).ItemStatus == ItemStatus.Good ? 1 : 0);
                        NodumpCount -= ((item as Rom).ItemStatus == ItemStatus.Nodump ? 1 : 0);
                        VerifiedCount -= ((item as Rom).ItemStatus == ItemStatus.Verified ? 1 : 0);
                        break;
                    case ItemType.Sample:
                        SampleCount--;
                        break;
                    case ItemType.SharedFeature:
                        SharedFeatureCount--;
                        break;
                    case ItemType.Slot:
                        SlotCount--;
                        break;
                    case ItemType.SoftwareList:
                        SoftwareListCount--;
                        break;
                    case ItemType.Sound:
                        SoundCount--;
                        break;
                }
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Generic constructor
        /// </summary>
        public ItemDictionaryDB()
        {
            bucketedBy = ItemKey.NULL;
            mergedBy = DedupeType.None;
            dbc = new SqliteConnection(ItemDictionaryConnectionString);
            logger = new Logger(this);
        }

        #endregion

        #region Database

        /// <summary>
        /// Ensure that the database exists and has the proper schema
        /// </summary>
        protected void EnsureDatabase()
        {
            // Make sure the file exists
            if (!File.Exists(ItemDictionaryFileName))
                File.Create(ItemDictionaryFileName);

            // If we have no database connection, we can't do anything
            if (dbc == null)
                return;

            // Open the database connection
            dbc.Open();

            // Make sure the database has the correct schema
            string query = @"
CREATE TABLE IF NOT EXISTS keys (
    'key'		TEXT		NOT NULL
    PRIMARY KEY (key)
)";
            SqliteCommand slc = new SqliteCommand(query, dbc);
            slc.ExecuteNonQuery();

            query = @"
CREATE TABLE IF NOT EXISTS groups (
    'key'		TEXT		NOT NULL
    'item`      TEXT        NOT NULL
)";

            slc = new SqliteCommand(query, dbc);
            slc.ExecuteNonQuery();

            slc.Dispose();
            dbc.Close();
        }

        #endregion

        #region Custom Functionality

        /// <summary>
        /// Take the arbitrarily bucketed Files Dictionary and convert to one bucketed by a user-defined method
        /// </summary>
        /// <param name="bucketBy">ItemKey enum representing how to bucket the individual items</param>
        /// <param name="dedupeType">Dedupe type that should be used</param>
        /// <param name="lower">True if the key should be lowercased (default), false otherwise</param>
        /// <param name="norename">True if games should only be compared on game and file name, false if system and source are counted</param>
        public void BucketBy(ItemKey bucketBy, DedupeType dedupeType, bool lower = true, bool norename = true)
        {
            // If we have a situation where there's no database or no keys at all, we skip
            if (dbc == null || Keys.Count == 0)
                return;

            // If the sorted type isn't the same, we want to sort the dictionary accordingly
            if (bucketedBy != bucketBy && bucketBy != ItemKey.NULL)
            {
                logger.User($"Organizing roms by {bucketBy}");

                // Set the sorted type
                bucketedBy = bucketBy;

                // Reset the merged type since this might change the merge
                mergedBy = DedupeType.None;

                // First do the initial sort of all of the roms inplace
                List<string> oldkeys = Keys.ToList();
                Parallel.For(0, oldkeys.Count, Globals.ParallelOptions, k =>
                {
                    string key = oldkeys[k];

                    // Now add each of the roms to their respective keys
                    for (int i = 0; i < this[key].Count; i++)
                    {
                        DatItem item = this[key][i];
                        if (item == null)
                            continue;

                        // We want to get the key most appropriate for the given sorting type
                        string newkey = item.GetKey(bucketBy, lower, norename);

                        // If the key is different, move the item to the new key
                        if (newkey != key)
                        {
                            Add(newkey, item);
                            Remove(key, item);
                            i--; // This make sure that the pointer stays on the correct since one was removed
                        }
                    }

                    // If the key is now empty, remove it
                    if (this[key].Count == 0)
                        Remove(key);
                });
            }

            // If the merge type isn't the same, we want to merge the dictionary accordingly
            if (mergedBy != dedupeType)
            {
                logger.User($"Deduping roms by {dedupeType}");

                // Set the sorted type
                mergedBy = dedupeType;

                List<string> keys = Keys.ToList();
                Parallel.ForEach(keys, Globals.ParallelOptions, key =>
                {
                    // Get the possibly unsorted list
                    ConcurrentList<DatItem> sortedlist = this[key].ToConcurrentList();

                    // Sort the list of items to be consistent
                    DatItem.Sort(ref sortedlist, false);

                    // If we're merging the roms, do so
                    if (dedupeType == DedupeType.Full || (dedupeType == DedupeType.Game && bucketBy == ItemKey.Machine))
                        sortedlist = DatItem.Merge(sortedlist);

                    // Add the list back to the dictionary
                    Reset(key);
                    AddRange(key, sortedlist);
                });
            }
            // If the merge type is the same, we want to sort the dictionary to be consistent
            else
            {
                List<string> keys = Keys.ToList();
                Parallel.ForEach(keys, Globals.ParallelOptions, key =>
                {
                    // Get the possibly unsorted list
                    ConcurrentList<DatItem> sortedlist = this[key];

                    // Sort the list of items to be consistent
                    DatItem.Sort(ref sortedlist, false);
                });
            }
        }

        /// <summary>
        /// Remove any keys that have null or empty values
        /// </summary>
        public void ClearEmpty()
        {
            var keys = Keys.Where(k => k != null).ToList();
            foreach (string key in keys)
            {
                // If the key doesn't exist, skip
                if (!Keys.Contains(key))
                    continue;

                // If the value is null, remove
                else if (this[key] == null)
                    this[key] = null;

                // If there are no non-blank items, remove
                else if (this[key].Count(i => i != null && i.ItemType != ItemType.Blank) == 0)
                    this[key] = null;
            }
        }

        /// <summary>
        /// Remove all items marked for removal
        /// </summary>
        public void ClearMarked()
        {
            var keys = Keys.ToList();
            foreach (string key in keys)
            {
                ConcurrentList<DatItem> oldItemList = this[key];
                ConcurrentList<DatItem> newItemList = oldItemList.Where(i => !i.Remove).ToConcurrentList();

                Remove(key);
                AddRange(key, newItemList);
            }
        }

        /// <summary>
        /// List all duplicates found in a DAT based on a DatItem
        /// </summary>
        /// <param name="datItem">Item to try to match</param>
        /// <param name="sorted">True if the DAT is already sorted accordingly, false otherwise (default)</param>
        /// <returns>List of matched DatItem objects</returns>
        public ConcurrentList<DatItem> GetDuplicates(DatItem datItem, bool sorted = false)
        {
            ConcurrentList<DatItem> output = new ConcurrentList<DatItem>();

            // Check for an empty rom list first
            if (TotalCount == 0)
                return output;

            // We want to get the proper key for the DatItem
            string key = SortAndGetKey(datItem, sorted);

            // If the key doesn't exist, return the empty list
            if (!ContainsKey(key))
                return output;

            // Try to find duplicates
            ConcurrentList<DatItem> roms = this[key];
            ConcurrentList<DatItem> left = new ConcurrentList<DatItem>();
            for (int i = 0; i < roms.Count; i++)
            {
                DatItem other = roms[i];
                if (other.Remove)
                    continue;

                if (datItem.Equals(other))
                {
                    other.Remove = true;
                    output.Add(other);
                }
                else
                {
                    left.Add(other);
                }
            }

            // Add back all roms with the proper flags
            Remove(key);
            AddRange(key, output);
            AddRange(key, left);

            return output;
        }

        /// <summary>
        /// Check if a DAT contains the given DatItem
        /// </summary>
        /// <param name="datItem">Item to try to match</param>
        /// <param name="sorted">True if the DAT is already sorted accordingly, false otherwise (default)</param>
        /// <returns>True if it contains the rom, false otherwise</returns>
        public bool HasDuplicates(DatItem datItem, bool sorted = false)
        {
            // Check for an empty rom list first
            if (TotalCount == 0)
                return false;

            // We want to get the proper key for the DatItem
            string key = SortAndGetKey(datItem, sorted);

            // If the key doesn't exist, return the empty list
            if (!ContainsKey(key))
                return false;

            // Try to find duplicates
            ConcurrentList<DatItem> roms = this[key];
            return roms.Any(r => datItem.Equals(r));
        }

        /// <summary>
        /// Recalculate the statistics for the Dat
        /// </summary>
        public void RecalculateStats()
        {
            // Wipe out any stats already there
            ResetStatistics();

            // If we have a blank Dat in any way, return
            if (dbc == null)
                return;

            // Loop through and add
            foreach (string key in Keys)
            {
                ConcurrentList<DatItem> datItems = this[key];
                foreach (DatItem item in datItems)
                {
                    AddItemStatistics(item);
                }
            }
        }

        /// <summary>
        /// Reset all statistics
        /// </summary>
        public void ResetStatistics()
        {
            TotalCount = 0;

            ArchiveCount = 0;
            BiosSetCount = 0;
            ChipCount = 0;
            DiskCount = 0;
            MediaCount = 0;
            ReleaseCount = 0;
            RomCount = 0;
            SampleCount = 0;

            GameCount = 0;

            TotalSize = 0;

            CRCCount = 0;
            MD5Count = 0;
            SHA1Count = 0;
            SHA256Count = 0;
            SHA384Count = 0;
            SHA512Count = 0;
            SpamSumCount = 0;

            BaddumpCount = 0;
            GoodCount = 0;
            NodumpCount = 0;
            RemovedCount = 0;
            VerifiedCount = 0;
        }

        /// <summary>
        /// Get the highest-order Field value that represents the statistics
        /// </summary>
        private ItemKey GetBestAvailable()
        {
            // If all items are supposed to have a SHA-512, we bucket by that
            if (DiskCount + MediaCount + RomCount - NodumpCount == SHA512Count)
                return ItemKey.SHA512;

            // If all items are supposed to have a SHA-384, we bucket by that
            else if (DiskCount + MediaCount + RomCount - NodumpCount == SHA384Count)
                return ItemKey.SHA384;

            // If all items are supposed to have a SHA-256, we bucket by that
            else if (DiskCount + MediaCount + RomCount - NodumpCount == SHA256Count)
                return ItemKey.SHA256;

            // If all items are supposed to have a SHA-1, we bucket by that
            else if (DiskCount + MediaCount + RomCount - NodumpCount == SHA1Count)
                return ItemKey.SHA1;

            // If all items are supposed to have a MD5, we bucket by that
            else if (DiskCount + MediaCount + RomCount - NodumpCount == MD5Count)
                return ItemKey.MD5;

            // Otherwise, we bucket by CRC
            else
                return ItemKey.CRC;
        }

        /// <summary>
        /// Sort the input DAT and get the key to be used by the item
        /// </summary>
        /// <param name="datItem">Item to try to match</param>
        /// <param name="sorted">True if the DAT is already sorted accordingly, false otherwise (default)</param>
        /// <returns>Key to try to use</returns>
        private string SortAndGetKey(DatItem datItem, bool sorted = false)
        {
            // If we're not already sorted, take care of it
            if (!sorted)
                BucketBy(GetBestAvailable(), DedupeType.None);

            // Now that we have the sorted type, we get the proper key
            return datItem.GetKey(bucketedBy);
        }

        #endregion

        #region IDictionary Implementations

        public ICollection<ConcurrentList<DatItem>> Values => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        public bool TryGetValue(string key, out ConcurrentList<DatItem> value) => throw new NotImplementedException();

        public void Add(KeyValuePair<string, ConcurrentList<DatItem>> item) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();

        public bool Contains(KeyValuePair<string, ConcurrentList<DatItem>> item) => throw new NotImplementedException();

        public void CopyTo(KeyValuePair<string, ConcurrentList<DatItem>>[] array, int arrayIndex) => throw new NotImplementedException();

        public bool Remove(KeyValuePair<string, ConcurrentList<DatItem>> item) => throw new NotImplementedException();

        public IEnumerator<KeyValuePair<string, ConcurrentList<DatItem>>> GetEnumerator() => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        #endregion
    }
}
