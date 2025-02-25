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
    /// Represents parsing and writing of an OfflineList XML DAT
    /// </summary>
    internal class OfflineList : DatFile
    {
        /// <summary>
        /// Constructor designed for casting a base DatFile
        /// </summary>
        /// <param name="datFile">Parent DatFile to copy from</param>
        public OfflineList(DatFile datFile)
            : base(datFile)
        {
        }

        /// <inheritdoc/>
        public override void ParseFile(string filename, int indexId, bool keep, bool statsOnly = false, bool throwOnError = false)
        {
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
                        case "configuration":
                            ReadConfiguration(xtr.ReadSubtree(), keep);

                            // Skip the configuration node now that we've processed it
                            xtr.Skip();
                            break;

                        case "games":
                            ReadGames(xtr.ReadSubtree(), statsOnly, filename, indexId);

                            // Skip the games node now that we've processed it
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
        /// Read configuration information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        /// <param name="keep">True if full pathnames are to be kept, false otherwise (default)</param>
        private void ReadConfiguration(XmlReader reader, bool keep)
        {
            bool superdat = false;

            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all configuration items (ONLY OVERWRITE IF THERE'S NO DATA)
                string content;
                switch (reader.Name.ToLowerInvariant())
                {
                    case "datname":
                        content = reader.ReadElementContentAsString();
                        Header.Name ??= content;
                        superdat |= content.Contains(" - SuperDAT");
                        if (keep && superdat)
                        {
                            Header.Type ??= "SuperDAT";
                        }
                        break;

                    case "datversion":
                        content = reader.ReadElementContentAsString();
                        Header.Version ??= content;
                        break;

                    case "system":
                        content = reader.ReadElementContentAsString();
                        Header.System ??= content;
                        break;

                    // TODO: Int32?
                    case "screenshotswidth":
                        content = reader.ReadElementContentAsString();
                        Header.ScreenshotsWidth ??= content;
                        break;

                    // TODO: Int32?
                    case "screenshotsheight":
                        content = reader.ReadElementContentAsString();
                        Header.ScreenshotsHeight ??= content;
                        break;

                    case "infos":
                        ReadInfos(reader.ReadSubtree());

                        // Skip the infos node now that we've processed it
                        reader.Skip();
                        break;

                    case "canopen":
                        ReadCanOpen(reader.ReadSubtree());

                        // Skip the canopen node now that we've processed it
                        reader.Skip();
                        break;

                    // TODO: Use all header values
                    case "newdat":
                        ReadNewDat(reader.ReadSubtree());

                        // Skip the newdat node now that we've processed it
                        reader.Skip();
                        break;

                    // TODO: Use header values
                    case "search":
                        ReadSearch(reader.ReadSubtree());

                        // Skip the search node now that we've processed it
                        reader.Skip();
                        break;

                    case "romtitle":
                        content = reader.ReadElementContentAsString();
                        Header.RomTitle ??= content;
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read infos information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        private void ReadInfos(XmlReader reader)
        {
            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Setup the infos object
            Header.Infos = new List<OfflineListInfo>();

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Add all infos to the info list
                switch (reader.Name.ToLowerInvariant())
                {
                    default:
                        var info = new OfflineListInfo
                        {
                            Name = reader.Name.ToLowerInvariant(),
                            Visible = reader.GetAttribute("visible").AsYesNo(),
                            InNamingOption = reader.GetAttribute("inNamingOption").AsYesNo(),
                            Default = reader.GetAttribute("default").AsYesNo()
                        };

                        Header.Infos.Add(info);

                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read canopen information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        private void ReadCanOpen(XmlReader reader)
        {
            // Prepare all internal variables
            Header.CanOpen = new List<string>();

            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all canopen items
                switch (reader.Name.ToLowerInvariant())
                {
                    case "extension":
                        Header.CanOpen.Add(reader.ReadElementContentAsString());
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read newdat information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        private void ReadNewDat(XmlReader reader)
        {
            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all newdat items
                string content;
                switch (reader.Name.ToLowerInvariant())
                {
                    case "datversionurl":
                        // TODO: Read this into an appropriate field
                        content = reader.ReadElementContentAsString();
                        Header.Url ??= content;
                        break;

                    case "daturl":
                        // TODO: Read this into an appropriate structure
                        reader.GetAttribute("fileName");
                        reader.ReadElementContentAsString();
                        break;

                    case "imurl":
                        // TODO: Read this into an appropriate field
                        reader.ReadElementContentAsString();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read search information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        private void ReadSearch(XmlReader reader)
        {
            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all search items
                switch (reader.Name.ToLowerInvariant())
                {
                    case "to":
                        // TODO: Read this into an appropriate structure
                        reader.GetAttribute("value");
                        reader.GetAttribute("default"); // (true|false)
                        reader.GetAttribute("auto"); // (true|false)

                        ReadTo(reader.ReadSubtree());

                        // Skip the to node now that we've processed it
                        reader.Skip();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read to information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        private void ReadTo(XmlReader reader)
        {
            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all search items
                switch (reader.Name.ToLowerInvariant())
                {
                    case "find":
                        // TODO: Read this into an appropriate structure
                        reader.GetAttribute("operation");
                        reader.GetAttribute("value"); // Int32?
                        reader.ReadElementContentAsString();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read games information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private void ReadGames(XmlReader reader, bool statsOnly, string filename, int indexId)
        {
            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all games items (ONLY OVERWRITE IF THERE'S NO DATA)
                switch (reader.Name.ToLowerInvariant())
                {
                    case "game":
                        ReadGame(reader.ReadSubtree(), statsOnly, filename, indexId);

                        // Skip the game node now that we've processed it
                        reader.Skip();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read game information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private void ReadGame(XmlReader reader, bool statsOnly, string filename, int indexId)
        {
            // Prepare all internal variables
            string releaseNumber = string.Empty, duplicateid;
            long? size = null;
            List<Rom> datItems = new List<Rom>();
            Machine machine = new Machine();

            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all games items
                switch (reader.Name.ToLowerInvariant())
                {
                    case "imagenumber":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "releasenumber":
                        // TODO: Read this into a field
                        releaseNumber = reader.ReadElementContentAsString();
                        break;

                    case "title":
                        machine.Name = reader.ReadElementContentAsString();
                        break;

                    case "savetype":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "romsize":
                        size = Utilities.CleanLong(reader.ReadElementContentAsString());
                        break;

                    case "publisher":
                        machine.Publisher = reader.ReadElementContentAsString();
                        break;

                    case "location":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "sourcerom":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "language":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "files":
                        datItems = ReadFiles(reader.ReadSubtree(), releaseNumber, machine.Name, filename, indexId);
                        
                        // Skip the files node now that we've processed it
                        reader.Skip();
                        break;

                    case "im1crc":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "im2crc":
                        // TODO: Read this into a field
                        reader.ReadElementContentAsString();
                        break;

                    case "comment":
                        machine.Comment = reader.ReadElementContentAsString();
                        break;

                    case "duplicateid":
                        duplicateid = reader.ReadElementContentAsString();
                        if (duplicateid != "0")
                            machine.CloneOf = duplicateid;

                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // Add information accordingly for each rom
            for (int i = 0; i < datItems.Count; i++)
            {
                datItems[i].Size = size;
                datItems[i].CopyMachineInformation(machine);

                // Now process and add the rom
                ParseAddHelper(datItems[i], statsOnly);
            }
        }

        /// <summary>
        /// Read files information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        /// <param name="releaseNumber">Release number from the parent game</param>
        /// <param name="machineName">Name of the parent game to use</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        private List<Rom> ReadFiles(
            XmlReader reader,
            string releaseNumber,
            string machineName,

            // Standard Dat parsing
            string filename,
            int indexId)
        {
            // Prepare all internal variables
            var extensionToCrc = new List<KeyValuePair<string, string>>();
            var roms = new List<Rom>();

            // If there's no subtree to the configuration, skip it
            if (reader == null)
                return roms;

            // Otherwise, add what is possible
            reader.MoveToContent();

            // Otherwise, read what we can from the header
            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get all romCRC items
                switch (reader.Name.ToLowerInvariant())
                {
                    case "romcrc":
                        extensionToCrc.Add(
                            new KeyValuePair<string, string>(
                                reader.GetAttribute("extension") ?? string.Empty,
                                reader.ReadElementContentAsString().ToLowerInvariant()));
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }

            // Now process the roms with the proper information
            foreach (KeyValuePair<string, string> pair in extensionToCrc)
            {
                roms.Add(new Rom()
                {
                    Name = (releaseNumber != "0" ? releaseNumber + " - " : string.Empty) + machineName + pair.Key,
                    CRC = pair.Value,

                    ItemStatus = ItemStatus.None,

                    Source = new Source
                    {
                        Index = indexId,
                        Name = filename,
                    },
                });
            }

            return roms;
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
            xtw.WriteStartDocument(false);

            xtw.WriteStartElement("dat");
            xtw.WriteAttributeString("xsi", "xmlns", "http://www.w3.org/2001/XMLSchema-instance");
            xtw.WriteAttributeString("noNamespaceSchemaLocation", "xsi", "datas.xsd");

            xtw.WriteStartElement("configuration");
            xtw.WriteRequiredElementString("datName", Header.Name);
            xtw.WriteElementString("datVersion", Items.TotalCount.ToString());
            xtw.WriteRequiredElementString("system", Header.System);
            xtw.WriteRequiredElementString("screenshotsWidth", Header.ScreenshotsWidth);
            xtw.WriteRequiredElementString("screenshotsHeight", Header.ScreenshotsHeight);

            if (Header.Infos != null)
            {
                xtw.WriteStartElement("infos");

                foreach (var info in Header.Infos)
                {
                    xtw.WriteStartElement(info.Name);
                    xtw.WriteAttributeString("visible", info.Visible?.ToString());
                    xtw.WriteAttributeString("inNamingOption", info.InNamingOption?.ToString());
                    xtw.WriteAttributeString("default", info.Default?.ToString());
                    xtw.WriteEndElement();
                }

                // End infos
                xtw.WriteEndElement();
            }

            if (Header.CanOpen != null)
            {
                xtw.WriteStartElement("canOpen");

                foreach (string extension in Header.CanOpen)
                {
                    xtw.WriteElementString("extension", extension);
                }

                // End canOpen
                xtw.WriteEndElement();
            }

            xtw.WriteStartElement("newDat");
            xtw.WriteRequiredElementString("datVersionURL", Header.Url);

            xtw.WriteStartElement("datUrl");
            xtw.WriteAttributeString("fileName", $"{Header.FileName ?? string.Empty}.zip");
            xtw.WriteString(Header.Url);
            xtw.WriteEndElement();

            xtw.WriteRequiredElementString("imURL", Header.Url);

            // End newDat
            xtw.WriteEndElement();

            xtw.WriteStartElement("search");

            xtw.WriteStartElement("to");
            xtw.WriteAttributeString("value", "location");
            xtw.WriteAttributeString("default", "true");
            xtw.WriteAttributeString("auto", "true");
            xtw.WriteEndElement();

            xtw.WriteStartElement("to");
            xtw.WriteAttributeString("value", "romSize");
            xtw.WriteAttributeString("default", "true");
            xtw.WriteAttributeString("auto", "false");
            xtw.WriteEndElement();

            xtw.WriteStartElement("to");
            xtw.WriteAttributeString("value", "languages");
            xtw.WriteAttributeString("default", "true");
            xtw.WriteAttributeString("auto", "true");
            xtw.WriteEndElement();

            xtw.WriteStartElement("to");
            xtw.WriteAttributeString("value", "saveType");
            xtw.WriteAttributeString("default", "false");
            xtw.WriteAttributeString("auto", "false");
            xtw.WriteEndElement();

            xtw.WriteStartElement("to");
            xtw.WriteAttributeString("value", "publisher");
            xtw.WriteAttributeString("default", "false");
            xtw.WriteAttributeString("auto", "true");
            xtw.WriteEndElement();

            xtw.WriteStartElement("to");
            xtw.WriteAttributeString("value", "sourceRom");
            xtw.WriteAttributeString("default", "false");
            xtw.WriteAttributeString("auto", "true");
            xtw.WriteEndElement();

            // End search
            xtw.WriteEndElement();

            xtw.WriteRequiredElementString("romTitle", Header.RomTitle ?? "%u - %n");

            // End configuration
            xtw.WriteEndElement();

            xtw.WriteStartElement("games");

            xtw.Flush();
        }

        /// <summary>
        /// Write out DatItem using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        /// <param name="datItem">DatItem object to be output</param>
        /// <returns>True if the data was written, false on error</returns>
        private void WriteDatItem(XmlTextWriter xtw, DatItem datItem)
        {
            // Pre-process the item name
            ProcessItemName(datItem, true);

            // Build the state
            xtw.WriteStartElement("game");
            xtw.WriteElementString("imageNumber", "1");
            xtw.WriteElementString("releaseNumber", "1");
            xtw.WriteRequiredElementString("title", datItem.GetName() ?? string.Empty);
            xtw.WriteElementString("saveType", "None");

            if (datItem.ItemType == ItemType.Rom)
            {
                var rom = datItem as Rom;
                xtw.WriteRequiredElementString("romSize", rom.Size?.ToString());
            }

            xtw.WriteRequiredElementString("publisher", datItem.Machine.Publisher);
            xtw.WriteElementString("location", "0");
            xtw.WriteElementString("sourceRom", "None");
            xtw.WriteElementString("language", "0");

            if (datItem.ItemType == ItemType.Rom)
            {
                var rom = datItem as Rom;
                string tempext = "." + rom.Name.GetNormalizedExtension();

                xtw.WriteStartElement("files");
                if (!string.IsNullOrWhiteSpace(rom.CRC))
                {
                    xtw.WriteStartElement("romCRC");
                    xtw.WriteRequiredAttributeString("extension", tempext);
                    xtw.WriteString(rom.CRC?.ToUpperInvariant());
                    xtw.WriteEndElement();
                }

                // End files
                xtw.WriteEndElement();
            }

            xtw.WriteElementString("im1CRC", "00000000");
            xtw.WriteElementString("im2CRC", "00000000");
            xtw.WriteRequiredElementString("comment", datItem.Machine.Comment);
            xtw.WriteRequiredElementString("duplicateID", datItem.Machine.CloneOf);

            // End game
            xtw.WriteEndElement();

            xtw.Flush();
        }

        /// <summary>
        /// Write out DAT footer using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        /// <returns>True if the data was written, false on error</returns>
        private void WriteFooter(XmlTextWriter xtw)
        {
            // End games
            xtw.WriteEndElement();

            xtw.WriteStartElement("gui");

            xtw.WriteStartElement("images");
            xtw.WriteAttributeString("width", "487");
            xtw.WriteAttributeString("height", "162");

            xtw.WriteStartElement("image");
            xtw.WriteAttributeString("x", "0");
            xtw.WriteAttributeString("y", "0");
            xtw.WriteAttributeString("width", "240");
            xtw.WriteAttributeString("height", "160");
            xtw.WriteEndElement();

            xtw.WriteStartElement("image");
            xtw.WriteAttributeString("x", "245");
            xtw.WriteAttributeString("y", "0");
            xtw.WriteAttributeString("width", "240");
            xtw.WriteAttributeString("height", "160");
            xtw.WriteEndElement();

            // End images
            xtw.WriteEndElement();

            // End gui
            xtw.WriteEndElement();

            // End dat
            xtw.WriteEndElement();

            xtw.Flush();
        }
    }
}
