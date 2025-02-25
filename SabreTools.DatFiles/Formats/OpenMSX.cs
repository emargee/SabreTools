﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using SabreTools.Core;
using SabreTools.Core.Tools;
using SabreTools.DatItems;
using SabreTools.DatItems.Formats;
using SabreTools.IO;

namespace SabreTools.DatFiles.Formats
{
    /// <summary>
    /// Represents parsing and writing of a openMSX softawre list XML DAT
    /// </summary>
    internal class OpenMSX : DatFile
    {
        /// <summary>
        /// DTD for original openMSX DATs
        /// </summary>
        private const string OpenMSXDTD = @"<!ELEMENT softwaredb (person*)>
<!ELEMENT software (title, genmsxid?, system, company,year,country,dump)>
<!ELEMENT title (#PCDATA)>
<!ELEMENT genmsxid (#PCDATA)>
<!ELEMENT system (#PCDATA)>
<!ELEMENT company (#PCDATA)>
<!ELEMENT year (#PCDATA)>
<!ELEMENT country (#PCDATA)>
<!ELEMENT dump (#PCDATA)>
";

        /// <summary>
        /// Constructor designed for casting a base DatFile
        /// </summary>
        /// <param name="datFile">Parent DatFile to copy from</param>
        public OpenMSX(DatFile datFile)
            : base(datFile)
        {
        }

        /// <inheritdoc/>
        public override void ParseFile(string filename, int indexId, bool keep, bool statsOnly = false, bool throwOnError = false)
        {
            // Prepare all internal variables
            XmlReader xtr = XmlReader.Create(filename, new XmlReaderSettings
            {
                CheckCharacters = false,
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                ValidationFlags = XmlSchemaValidationFlags.None,
                ValidationType = ValidationType.None,
            });

            // If we got a null reader, just return
            if (xtr == null)
                return;

            // Otherwise, read the file to the end
            try
            {
                xtr.MoveToContent();
                while (!xtr.EOF)
                {
                    // We only want elements
                    if (xtr.NodeType != XmlNodeType.Element)
                    {
                        xtr.Read();
                        continue;
                    }

                    switch (xtr.Name)
                    {
                        case "softwaredb":
                            Header.Name ??= "openMSX Software List";
                            Header.Description ??= Header.Name;
                            Header.Date ??= xtr.GetAttribute("timestamp");
                            xtr.Read();
                            break;

                        // We want to process the entire subtree of the software
                        case "software":
                            ReadSoftware(xtr.ReadSubtree(), statsOnly, filename, indexId);

                            // Skip the software now that we've processed it
                            xtr.Skip();
                            break;

                        default:
                            xtr.Read();
                            break;
                    }
                }
            }
            catch (Exception ex) when (!throwOnError)
            {
                logger.Warning(ex, $"Exception found while parsing '{filename}'");

                // For XML errors, just skip the affected node
                xtr?.Read();
            }

            xtr.Dispose();
        }

        /// <summary>
        /// Read software information
        /// </summary>
        /// <param name="reader">XmlReader representing a machine block</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private void ReadSoftware(XmlReader reader, bool statsOnly, string filename, int indexId)
        {
            // If we have an empty machine, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            int diskno = 0;
            bool containsItems = false;

            // Create a new machine
            Machine machine = new Machine();

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get the roms from the machine
                switch (reader.Name)
                {
                    case "title":
                        machine.Name = reader.ReadElementContentAsString();
                        break;

                    case "genmsxid":
                        machine.GenMSXID = reader.ReadElementContentAsString();
                        break;

                    case "system":
                        machine.System = reader.ReadElementContentAsString();
                        break;

                    case "company":
                        machine.Manufacturer = reader.ReadElementContentAsString();
                        break;

                    case "year":
                        machine.Year = reader.ReadElementContentAsString();
                        break;

                    case "country":
                        machine.Country = reader.ReadElementContentAsString();
                        break;

                    case "dump":
                        containsItems = ReadDump(reader.ReadSubtree(), machine, diskno, statsOnly, filename, indexId);
                        diskno++;

                        // Skip the dump now that we've processed it
                        reader.Skip();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // If no items were found for this machine, add a Blank placeholder
            if (!containsItems)
            {
                Blank blank = new Blank()
                {
                    Source = new Source
                    {
                        Index = indexId,
                        Name = filename,
                    },
                };

                blank.CopyMachineInformation(machine);

                // Now process and add the rom
                ParseAddHelper(blank, statsOnly);
            }
        }

        /// <summary>
        /// Read dump information
        /// </summary>
        /// <param name="reader">XmlReader representing a part block</param>
        /// <param name="machine">Machine information to pass to contained items</param>
        /// <param name="diskno">Disk number to use when outputting to other DAT formats</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private bool ReadDump(
            XmlReader reader,
            Machine machine,
            int diskno,
            bool statsOnly,

            // Standard Dat parsing
            string filename,
            int indexId)
        {
            List<DatItem> items = new List<DatItem>();
            Original original = null;

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get the elements from the dump
                switch (reader.Name)
                {
                    case "rom":
                        DatItem rom = ReadRom(reader.ReadSubtree(), machine, diskno, filename, indexId);
                        if (rom != null)
                            items.Add(rom);

                        // Skip the rom now that we've processed it
                        reader.Skip();
                        break;

                    case "megarom":
                        DatItem megarom = ReadMegaRom(reader.ReadSubtree(), machine, diskno, filename, indexId);
                        if (megarom != null)
                            items.Add(megarom);

                        // Skip the megarom now that we've processed it
                        reader.Skip();
                        break;

                    case "sccpluscart":
                        DatItem sccpluscart = ReadSccPlusCart(reader.ReadSubtree(), machine, diskno, filename, indexId);
                        if (sccpluscart != null)
                            items.Add(sccpluscart);

                        // Skip the sccpluscart now that we've processed it
                        reader.Skip();
                        break;

                    case "original":
                        original = new Original
                        {
                            Value = reader.GetAttribute("value").AsYesNo(),
                            Content = reader.ReadElementContentAsString()
                        };
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // If we have any items, loop through and add them
            foreach (DatItem item in items)
            {
                switch (item.ItemType)
                {
                    case ItemType.Rom:
                        (item as Rom).Original = original;
                        break;
                }

                item.CopyMachineInformation(machine);
                ParseAddHelper(item, statsOnly);
            }

            return items.Count > 0;
        }

        /// <summary>
        /// Read rom information
        /// </summary>
        /// <param name="reader">XmlReader representing a rom block</param>
        /// <param name="machine">Machine information to pass to contained items</param>
        /// <param name="diskno">Disk number to use when outputting to other DAT formats</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private DatItem ReadRom(
            XmlReader reader,
            Machine machine,
            int diskno,

            // Standard Dat parsing
            string filename,
            int indexId)
        {
            string hash = string.Empty,
                offset = string.Empty,
                type = string.Empty,
                remark = string.Empty;

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get the elements from the rom
                switch (reader.Name)
                {
                    case "hash":
                        hash = reader.ReadElementContentAsString();
                        break;

                    case "start":
                        offset = reader.ReadElementContentAsString();
                        break;

                    case "type":
                        type = reader.ReadElementContentAsString();
                        break;

                    case "remark":
                        remark = reader.ReadElementContentAsString();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // If we got a hash, then create and return the item
            if (!string.IsNullOrWhiteSpace(hash))
            {
                return new Rom
                {
                    Name = machine.Name + "_" + diskno + (!string.IsNullOrWhiteSpace(remark) ? " " + remark : string.Empty),
                    Offset = offset,
                    Size = null,
                    SHA1 = hash,

                    Source = new Source
                    {
                        Index = indexId,
                        Name = filename,
                    },

                    OpenMSXSubType = OpenMSXSubType.Rom,
                    OpenMSXType = type,
                    Remark = remark,
                };
            }

            // No valid item means returning null
            return null;
        }

        /// <summary>
        /// Read megarom information
        /// </summary>
        /// <param name="reader">XmlReader representing a megarom block</param>
        /// <param name="machine">Machine information to pass to contained items</param>
        /// <param name="diskno">Disk number to use when outputting to other DAT formats</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private DatItem ReadMegaRom(
            XmlReader reader,
            Machine machine,
            int diskno,

            // Standard Dat parsing
            string filename,
            int indexId)
        {
            string hash = string.Empty,
                offset = string.Empty,
                type = string.Empty,
                remark = string.Empty;

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get the elements from the dump
                switch (reader.Name)
                {
                    case "hash":
                        hash = reader.ReadElementContentAsString();
                        break;

                    case "start":
                        offset = reader.ReadElementContentAsString();
                        break;

                    case "type":
                        reader.ReadElementContentAsString();
                        break;

                    case "remark":
                        remark = reader.ReadElementContentAsString();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // If we got a hash, then create and return the item
            if (!string.IsNullOrWhiteSpace(hash))
            {
                return new Rom
                {
                    Name = machine.Name + "_" + diskno + (!string.IsNullOrWhiteSpace(remark) ? " " + remark : string.Empty),
                    Offset = offset,
                    Size = null,
                    SHA1 = hash,

                    Source = new Source
                    {
                        Index = indexId,
                        Name = filename,
                    },

                    OpenMSXSubType = OpenMSXSubType.MegaRom,
                    OpenMSXType = type,
                    Remark = remark,
                };
            }

            // No valid item means returning null
            return null;
        }

        /// <summary>
        /// Read sccpluscart information
        /// </summary>
        /// <param name="reader">XmlReader representing a sccpluscart block</param>
        /// <param name="machine">Machine information to pass to contained items</param>
        /// <param name="diskno">Disk number to use when outputting to other DAT formats</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private DatItem ReadSccPlusCart(
            XmlReader reader,
            Machine machine,
            int diskno,

            // Standard Dat parsing
            string filename,
            int indexId)
        {
            string boot = string.Empty,
                hash = string.Empty,
                remark = string.Empty;

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get the elements from the dump
                switch (reader.Name)
                {
                    case "boot":
                        boot = reader.ReadElementContentAsString();
                        break;

                    case "hash":
                        hash = reader.ReadElementContentAsString();
                        break;

                    case "remark":
                        remark = reader.ReadElementContentAsString();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // If we got a hash, then create and return the item
            if (!string.IsNullOrWhiteSpace(hash))
            {
                return new Rom
                {
                    Name = machine.Name + "_" + diskno + (!string.IsNullOrWhiteSpace(remark) ? " " + remark : string.Empty),
                    Size = null,
                    SHA1 = hash,

                    Source = new Source
                    {
                        Index = indexId,
                        Name = filename,
                    },

                    OpenMSXSubType = OpenMSXSubType.SCCPlusCart,
                    Boot = boot,
                    Remark = remark,
                };
            }

            // No valid item means returning null
            return null;
        }

        /// <inheritdoc/>
        protected override ItemType[] GetSupportedTypes()
        {
            return new ItemType[] { ItemType.Rom };
        }

        /// <inheritdoc/>
        protected override List<DatItemField> GetMissingRequiredFields(DatItem datItem)
        {
            // TODO: Check required fields
            return null;
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

                XmlTextWriter xtw = new XmlTextWriter(fs, new UTF8Encoding(false))
                {
                    Formatting = Formatting.Indented,
                    IndentChar = '\t',
                    Indentation = 1
                };

                // Write out the header
                WriteHeader(xtw);

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
                            WriteEndGame(xtw);

                        // If we have a new game, output the beginning of the new item
                        if (lastgame == null || lastgame.ToLowerInvariant() != datItem.Machine.Name.ToLowerInvariant())
                            WriteStartGame(xtw, datItem);

                        // Check for a "null" item
                        datItem = ProcessNullifiedItem(datItem);

                        // Write out the item if we're not ignoring
                        if (!ShouldIgnore(datItem, ignoreblanks))
                            WriteDatItem(xtw, datItem);

                        // Set the new data to compare against
                        lastgame = datItem.Machine.Name;
                    }
                }

                // Write the file footer out
                WriteFooter(xtw);

                logger.User($"'{outfile}' written!{Environment.NewLine}");
                xtw.Dispose();
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
        /// Write out DAT header using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        private void WriteHeader(XmlTextWriter xtw)
        {
            xtw.WriteStartDocument();
            xtw.WriteDocType("softwaredb", null, "softwaredb1.dtd", null);

            xtw.WriteStartElement("softwaredb");
            xtw.WriteRequiredAttributeString("timestamp", Header.Date);

            //TODO: Figure out how to fix the issue with removed formatting after this point
//                xtw.WriteComment("Credits");
//                xtw.WriteCData(@"The softwaredb.xml file contains information about rom mapper types

//-Copyright 2003 Nicolas Beyaert(Initial Database)
//-Copyright 2004 - 2013 BlueMSX Team
//-Copyright 2005 - 2020 openMSX Team
//-Generation MSXIDs by www.generation - msx.nl

//- Thanks go out to:
//-Generation MSX / Sylvester for the incredible source of information
//- p_gimeno and diedel for their help adding and valdiating ROM additions
//- GDX for additional ROM info and validations and corrections");

            xtw.Flush();
        }

        /// <summary>
        /// Write out Game start using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        /// <param name="datItem">DatItem object to be output</param>
        private void WriteStartGame(XmlTextWriter xtw, DatItem datItem)
        {
            // No game should start with a path separator
            datItem.Machine.Name = datItem.Machine.Name.TrimStart(Path.DirectorySeparatorChar);

            // Build the state
            xtw.WriteStartElement("software");
            xtw.WriteRequiredElementString("title", datItem.Machine.Name);
            xtw.WriteRequiredElementString("genmsxid", datItem.Machine.GenMSXID);
            xtw.WriteRequiredElementString("system", datItem.Machine.System);
            xtw.WriteRequiredElementString("company", datItem.Machine.Manufacturer);
            xtw.WriteRequiredElementString("year", datItem.Machine.Year);
            xtw.WriteRequiredElementString("country", datItem.Machine.Country);

            xtw.Flush();
        }

        /// <summary>
        /// Write out Game start using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        private void WriteEndGame(XmlTextWriter xtw)
        {
            // End software
            xtw.WriteEndElement();

            xtw.Flush();
        }

        /// <summary>
        /// Write out DatItem using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        /// <param name="datItem">DatItem object to be output</param>
        private void WriteDatItem(XmlTextWriter xtw, DatItem datItem)
        {
            // Pre-process the item name
            ProcessItemName(datItem, true);

            // Build the state
            switch (datItem.ItemType)
            {
                case ItemType.Rom:
                    var rom = datItem as Rom;
                    xtw.WriteStartElement("dump");

                    if (rom.Original != null)
                    {
                        xtw.WriteStartElement("original");
                        xtw.WriteAttributeString("value", rom.Original.Value == true ? "true" : "false");
                        xtw.WriteString(rom.Original.Content);
                        xtw.WriteEndElement();
                    }

                    switch (rom.OpenMSXSubType)
                    {
                        // Default to Rom for converting from other formats
                        case OpenMSXSubType.Rom:
                        case OpenMSXSubType.NULL:
                            xtw.WriteStartElement(rom.OpenMSXSubType.FromOpenMSXSubType());
                            xtw.WriteRequiredElementString("hash", rom.SHA1?.ToLowerInvariant());
                            xtw.WriteOptionalElementString("start", rom.Offset);
                            xtw.WriteOptionalElementString("type", rom.OpenMSXType);
                            xtw.WriteOptionalElementString("remark", rom.Remark);
                            xtw.WriteEndElement();
                            break;

                        case OpenMSXSubType.MegaRom:
                            xtw.WriteStartElement(rom.OpenMSXSubType.FromOpenMSXSubType());
                            xtw.WriteRequiredElementString("hash", rom.SHA1?.ToLowerInvariant());
                            xtw.WriteOptionalElementString("start", rom.Offset);
                            xtw.WriteOptionalElementString("type", rom.OpenMSXType);
                            xtw.WriteOptionalElementString("remark", rom.Remark);
                            xtw.WriteEndElement();
                            break;

                        case OpenMSXSubType.SCCPlusCart:
                            xtw.WriteStartElement(rom.OpenMSXSubType.FromOpenMSXSubType());
                            xtw.WriteOptionalElementString("boot", rom.Boot);
                            xtw.WriteRequiredElementString("hash", rom.SHA1?.ToLowerInvariant());
                            xtw.WriteOptionalElementString("remark", rom.Remark);
                            xtw.WriteEndElement();
                            break;
                    }

                    // End dump
                    xtw.WriteEndElement();
                    break;
            }

            xtw.Flush();
        }

        /// <summary>
        /// Write out DAT footer using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        private void WriteFooter(XmlTextWriter xtw)
        {
            // End software
            xtw.WriteEndElement();

            // End softwaredb
            xtw.WriteEndElement();

            xtw.Flush();
        }
    }
}
