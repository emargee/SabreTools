﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SabreTools.Core;
using SabreTools.Core.Tools;
using SabreTools.DatItems;
using SabreTools.DatItems.Formats;

namespace SabreTools.DatFiles.Formats
{
    /// <summary>
    /// Represents parsing and writing of a reference SabreDAT JSON
    /// </summary>
    internal class SabreJSON : DatFile
    {
        /// <summary>
        /// Constructor designed for casting a base DatFile
        /// </summary>
        /// <param name="datFile">Parent DatFile to copy from</param>
        public SabreJSON(DatFile datFile)
            : base(datFile)
        {
        }

        /// <inheritdoc/>
        public override void ParseFile(string filename, int indexId, bool keep, bool statsOnly = false, bool throwOnError = false)
        {
            // Prepare all internal variables
            StreamReader sr = new StreamReader(File.OpenRead(filename), new UTF8Encoding(false));
            JsonTextReader jtr = new JsonTextReader(sr);

            // If we got a null reader, just return
            if (jtr == null)
                return;

            // Otherwise, read the file to the end
            try
            {
                jtr.Read();
                while (!sr.EndOfStream)
                {
                    // Skip everything not a property name
                    if (jtr.TokenType != JsonToken.PropertyName)
                    {
                        jtr.Read();
                        continue;
                    }

                    switch (jtr.Value)
                    {
                        // Header value
                        case "header":
                            ReadHeader(jtr);
                            jtr.Read();
                            break;

                        // Machine array
                        case "machines":
                            ReadMachines(jtr, statsOnly, filename, indexId);
                            jtr.Read();
                            break;

                        default:
                            jtr.Read();
                            break;
                    }
                }
            }
            catch (Exception ex) when (!throwOnError)
            {
                logger.Warning($"Exception found while parsing '{filename}': {ex}");
            }

            jtr.Close();
        }

        /// <summary>
        /// Read header information
        /// </summary>
        /// <param name="jtr">JsonTextReader to use to parse the header</param>
        private void ReadHeader(JsonTextReader jtr)
        {
            // If the reader is invalid, skip
            if (jtr == null)
                return;

            // Read in the header and apply any new fields
            jtr.Read();
            JsonSerializer js = new JsonSerializer();
            DatHeader header = js.Deserialize<DatHeader>(jtr);
            Header.ConditionalCopy(header);
        }

        /// <summary>
        /// Read machine array information
        /// </summary>
        /// <param name="itr">JsonTextReader to use to parse the machine</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private void ReadMachines(JsonTextReader jtr, bool statsOnly, string filename, int indexId)
        {
            // If the reader is invalid, skip
            if (jtr == null)
                return;

            // Read in the machine array
            jtr.Read();
            JsonSerializer js = new JsonSerializer();
            JArray machineArray = js.Deserialize<JArray>(jtr);

            // Loop through each machine object and process
            foreach (JObject machineObj in machineArray)
            {
                ReadMachine(machineObj, statsOnly, filename, indexId);
            }
        }

        /// <summary>
        /// Read machine object information
        /// </summary>
        /// <param name="machineObj">JObject representing a single machine</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private void ReadMachine(JObject machineObj, bool statsOnly, string filename, int indexId)
        {
            // If object is invalid, skip it
            if (machineObj == null)
                return;

            // Prepare internal variables
            Machine machine = null;

            // Read the machine info, if possible
            if (machineObj.ContainsKey("machine"))
                machine = machineObj["machine"].ToObject<Machine>();

            // Read items, if possible
            if (machineObj.ContainsKey("items"))
                ReadItems(machineObj["items"] as JArray, statsOnly, filename, indexId, machine);
        }

        /// <summary>
        /// Read item array information
        /// </summary>
        /// <param name="itemsArr">JArray representing the items list</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        /// <param name="machine">Machine information to add to the parsed items</param>
        private void ReadItems(
            JArray itemsArr,
            bool statsOnly,

            // Standard Dat parsing
            string filename,
            int indexId,

            // Miscellaneous
            Machine machine)
        {
            // If the array is invalid, skip
            if (itemsArr == null)
                return;

            // Loop through each datitem object and process
            foreach (JObject itemObj in itemsArr)
            {
                ReadItem(itemObj, statsOnly, filename, indexId, machine);
            }
        }

        /// <summary>
        /// Read item information
        /// </summary>
        /// <param name="machineObj">JObject representing a single datitem</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        /// <param name="machine">Machine information to add to the parsed items</param>
        private void ReadItem(
            JObject itemObj,
            bool statsOnly,

            // Standard Dat parsing
            string filename,
            int indexId,

            // Miscellaneous
            Machine machine)
        {
            // If we have an empty item, skip it
            if (itemObj == null)
                return;

            // Prepare internal variables
            DatItem datItem = null;

            // Read the datitem info, if possible
            if (itemObj.ContainsKey("datitem"))
            {
                JToken datItemObj = itemObj["datitem"];
                switch (datItemObj.Value<string>("type").AsItemType())
                {
                    case ItemType.Adjuster:
                        datItem = datItemObj.ToObject<Adjuster>();
                        break;
                    case ItemType.Analog:
                        datItem = datItemObj.ToObject<Analog>();
                        break;
                    case ItemType.Archive:
                        datItem = datItemObj.ToObject<Archive>();
                        break;
                    case ItemType.BiosSet:
                        datItem = datItemObj.ToObject<BiosSet>();
                        break;
                    case ItemType.Blank:
                        datItem = datItemObj.ToObject<Blank>();
                        break;
                    case ItemType.Chip:
                        datItem = datItemObj.ToObject<Chip>();
                        break;
                    case ItemType.Condition:
                        datItem = datItemObj.ToObject<Condition>();
                        break;
                    case ItemType.Configuration:
                        datItem = datItemObj.ToObject<Configuration>();
                        break;
                    case ItemType.Control:
                        datItem = datItemObj.ToObject<Control>();
                        break;
                    case ItemType.DataArea:
                        datItem = datItemObj.ToObject<DataArea>();
                        break;
                    case ItemType.Device:
                        datItem = datItemObj.ToObject<Device>();
                        break;
                    case ItemType.DeviceReference:
                        datItem = datItemObj.ToObject<DeviceReference>();
                        break;
                    case ItemType.DipSwitch:
                        datItem = datItemObj.ToObject<DipSwitch>();
                        break;
                    case ItemType.Disk:
                        datItem = datItemObj.ToObject<Disk>();
                        break;
                    case ItemType.DiskArea:
                        datItem = datItemObj.ToObject<DiskArea>();
                        break;
                    case ItemType.Display:
                        datItem = datItemObj.ToObject<Display>();
                        break;
                    case ItemType.Driver:
                        datItem = datItemObj.ToObject<Driver>();
                        break;
                    case ItemType.Extension:
                        datItem = datItemObj.ToObject<Extension>();
                        break;
                    case ItemType.Feature:
                        datItem = datItemObj.ToObject<Feature>();
                        break;
                    case ItemType.Info:
                        datItem = datItemObj.ToObject<Info>();
                        break;
                    case ItemType.Input:
                        datItem = datItemObj.ToObject<Input>();
                        break;
                    case ItemType.Instance:
                        datItem = datItemObj.ToObject<Instance>();
                        break;
                    case ItemType.Location:
                        datItem = datItemObj.ToObject<Location>();
                        break;
                    case ItemType.Media:
                        datItem = datItemObj.ToObject<Media>();
                        break;
                    case ItemType.Part:
                        datItem = datItemObj.ToObject<Part>();
                        break;
                    case ItemType.PartFeature:
                        datItem = datItemObj.ToObject<PartFeature>();
                        break;
                    case ItemType.Port:
                        datItem = datItemObj.ToObject<Port>();
                        break;
                    case ItemType.RamOption:
                        datItem = datItemObj.ToObject<RamOption>();
                        break;
                    case ItemType.Release:
                        datItem = datItemObj.ToObject<Release>();
                        break;
                    case ItemType.Rom:
                        datItem = datItemObj.ToObject<Rom>();
                        break;
                    case ItemType.Sample:
                        datItem = datItemObj.ToObject<Sample>();
                        break;
                    case ItemType.Setting:
                        datItem = datItemObj.ToObject<Setting>();
                        break;
                    case ItemType.SharedFeature:
                        datItem = datItemObj.ToObject<SharedFeature>();
                        break;
                    case ItemType.Slot:
                        datItem = datItemObj.ToObject<Slot>();
                        break;
                    case ItemType.SlotOption:
                        datItem = datItemObj.ToObject<SlotOption>();
                        break;
                    case ItemType.SoftwareList:
                        datItem = datItemObj.ToObject<DatItems.Formats.SoftwareList>();
                        break;
                    case ItemType.Sound:
                        datItem = datItemObj.ToObject<Sound>();
                        break;
                }
            }

            // If we got a valid datitem, copy machine info and add
            if (datItem != null)
            {
                datItem.CopyMachineInformation(machine);
                datItem.Source = new Source { Index = indexId, Name = filename };
                ParseAddHelper(datItem, statsOnly);
            }
        }

        /// <inheritdoc/>
        public override bool WriteToFile(string outfile, bool ignoreblanks = false, bool throwOnError = false)
        {
            try
            {
                logger.User($"Writing to '{outfile}'...");
                FileStream fs = File.Create(outfile);

                // If we get back null for some reason, just log and return
                if (fs == null)
                {
                    logger.Warning($"File '{outfile}' could not be created for writing! Please check to see if the file is writable");
                    return false;
                }

                StreamWriter sw = new StreamWriter(fs, new UTF8Encoding(false));
                JsonTextWriter jtw = new JsonTextWriter(sw)
                {
                    Formatting = Formatting.Indented,
                    IndentChar = '\t',
                    Indentation = 1
                };

                // Write out the header
                WriteHeader(jtw);

                // Write out each of the machines and roms
                string lastgame = null;

                // Use a sorted list of games to output
                foreach (string key in Items.SortedKeys)
                {
                    ConcurrentList<DatItem> datItems = Items.FilteredItems(key);

                    // If this machine doesn't contain any writable items, skip
                    if (!ContainsWritable(datItems))
                        continue;

                    // Resolve the names in the block
                    datItems = DatItem.ResolveNames(datItems);

                    for (int index = 0; index < datItems.Count; index++)
                    {
                        DatItem datItem = datItems[index];

                        // If we have a different game and we're not at the start of the list, output the end of last item
                        if (lastgame != null && lastgame.ToLowerInvariant() != datItem.Machine.Name.ToLowerInvariant())
                            WriteEndGame(jtw);

                        // If we have a new game, output the beginning of the new item
                        if (lastgame == null || lastgame.ToLowerInvariant() != datItem.Machine.Name.ToLowerInvariant())
                            WriteStartGame(jtw, datItem);

                        // Check for a "null" item
                        datItem = ProcessNullifiedItem(datItem);

                        // Write out the item if we're not ignoring
                        if (!ShouldIgnore(datItem, ignoreblanks))
                            WriteDatItem(jtw, datItem);

                        // Set the new data to compare against
                        lastgame = datItem.Machine.Name;
                    }
                }

                // Write the file footer out
                WriteFooter(jtw);

                logger.User($"'{outfile}' written!{Environment.NewLine}");
                jtw.Close();
                fs.Dispose();
            }
            catch (Exception ex) when (!throwOnError)
            {
                logger.Error(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Write out DAT header using the supplied JsonTextWriter
        /// </summary>
        /// <param name="jtw">JsonTextWriter to output to</param>
        private void WriteHeader(JsonTextWriter jtw)
        {
            jtw.WriteStartObject();

            // Write the DatHeader
            jtw.WritePropertyName("header");
            JsonSerializer js = new JsonSerializer() { Formatting = Formatting.Indented };
            js.Serialize(jtw, Header);

            jtw.WritePropertyName("machines");
            jtw.WriteStartArray();

            jtw.Flush();
        }

        /// <summary>
        /// Write out Game start using the supplied JsonTextWriter
        /// </summary>
        /// <param name="jtw">JsonTextWriter to output to</param>
        /// <param name="datItem">DatItem object to be output</param>
        private void WriteStartGame(JsonTextWriter jtw, DatItem datItem)
        {
            // No game should start with a path separator
            datItem.Machine.Name = datItem.Machine.Name.TrimStart(Path.DirectorySeparatorChar) ?? string.Empty;

            // Build the state
            jtw.WriteStartObject();

            // Write the Machine
            jtw.WritePropertyName("machine");
            JsonSerializer js = new JsonSerializer() { Formatting = Formatting.Indented };
            js.Serialize(jtw, datItem.Machine);

            jtw.WritePropertyName("items");
            jtw.WriteStartArray();

            jtw.Flush();
        }

        /// <summary>
        /// Write out Game end using the supplied JsonTextWriter
        /// </summary>
        /// <param name="jtw">JsonTextWriter to output to</param>
        private void WriteEndGame(JsonTextWriter jtw)
        {
            // End items
            jtw.WriteEndArray();

            // End machine
            jtw.WriteEndObject();

            jtw.Flush();
        }

        /// <summary>
        /// Write out DatItem using the supplied JsonTextWriter
        /// </summary>
        /// <param name="jtw">JsonTextWriter to output to</param>
        /// <param name="datItem">DatItem object to be output</param>
        private void WriteDatItem(JsonTextWriter jtw, DatItem datItem)
        {
            // Pre-process the item name
            ProcessItemName(datItem, true);

            // Build the state
            jtw.WriteStartObject();

            // Write the DatItem
            jtw.WritePropertyName("datitem");
            JsonSerializer js = new JsonSerializer() { ContractResolver = new BaseFirstContractResolver(), Formatting = Formatting.Indented };
            js.Serialize(jtw, datItem);

            // End item
            jtw.WriteEndObject();

            jtw.Flush();
        }

        /// <summary>
        /// Write out DAT footer using the supplied JsonTextWriter
        /// </summary>
        /// <param name="jtw">JsonTextWriter to output to</param>
        private void WriteFooter(JsonTextWriter jtw)
        {
            // End items
            jtw.WriteEndArray();

            // End machine
            jtw.WriteEndObject();

            // End machines
            jtw.WriteEndArray();

            // End file
            jtw.WriteEndObject();

            jtw.Flush();
        }
    
        // https://github.com/dotnet/runtime/issues/728
        private class BaseFirstContractResolver : DefaultContractResolver
        {
            public BaseFirstContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy();
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, memberSerialization)
                    .OrderBy(p => BaseTypesAndSelf(p.DeclaringType).Count()).ToList();

                static IEnumerable<Type> BaseTypesAndSelf(Type t)
                {
                    while (t != null)
                    {
                        yield return t;
                        t = t.BaseType;
                    }
                }
            }
        }
    }
}
