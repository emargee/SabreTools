﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    /// Represents parsing and writing of a Logiqx-derived DAT
    /// </summary>
    internal class Logiqx : DatFile
    {
        // Private instance variables specific to Logiqx DATs
        private readonly bool _deprecated;

        /// <summary>
        /// DTD for original Logiqx DATs
        /// </summary>
        private const string LogiqxDTD = @"<!--
   ROM Management Datafile - DTD

   For further information, see: http://www.logiqx.com/

   This DTD module is identified by the PUBLIC and SYSTEM identifiers:

   PUBLIC string.Empty -//Logiqx//DTD ROM Management Datafile//ENstring.Empty
   SYSTEM string.Emptyhttp://www.logiqx.com/Dats/datafile.dtdstring.Empty

   $Revision: 1.5 $
   $Date: 2008/10/28 21:39:16 $

-->

<!ELEMENT datafile(header?, game*, machine*)>
    <!ATTLIST datafile build CDATA #IMPLIED>
    <!ATTLIST datafile debug (yes|no) string.Emptynostring.Empty>
    <!ELEMENT header(name, description, category?, version, date?, author, email?, homepage?, url?, comment?, clrmamepro?, romcenter?)>
        <!ELEMENT name(#PCDATA)>
        <!ELEMENT description (#PCDATA)>
        <!ELEMENT category (#PCDATA)>
        <!ELEMENT version (#PCDATA)>
        <!ELEMENT date (#PCDATA)>
        <!ELEMENT author (#PCDATA)>
        <!ELEMENT email (#PCDATA)>
        <!ELEMENT homepage (#PCDATA)>
        <!ELEMENT url (#PCDATA)>
        <!ELEMENT comment (#PCDATA)>
        <!ELEMENT clrmamepro EMPTY>
            <!ATTLIST clrmamepro header CDATA #IMPLIED>
            <!ATTLIST clrmamepro forcemerging (none|split|full) string.Emptysplitstring.Empty>
            <!ATTLIST clrmamepro forcenodump(obsolete|required|ignore) string.Emptyobsoletestring.Empty>
            <!ATTLIST clrmamepro forcepacking(zip|unzip) string.Emptyzipstring.Empty>
        <!ELEMENT romcenter EMPTY>
            <!ATTLIST romcenter plugin CDATA #IMPLIED>
            <!ATTLIST romcenter rommode (merged|split|unmerged) string.Emptysplitstring.Empty>
            <!ATTLIST romcenter biosmode(merged|split|unmerged) string.Emptysplitstring.Empty>
            <!ATTLIST romcenter samplemode(merged|unmerged) string.Emptymergedstring.Empty>
            <!ATTLIST romcenter lockrommode(yes|no) string.Emptynostring.Empty>
            <!ATTLIST romcenter lockbiosmode(yes|no) string.Emptynostring.Empty>
            <!ATTLIST romcenter locksamplemode(yes|no) string.Emptynostring.Empty>
    <!ELEMENT game(comment*, description, year?, manufacturer?, release*, biosset*, rom*, disk*, sample*, archive*)>
        <!ATTLIST game name CDATA #REQUIRED>
        <!ATTLIST game sourcefile CDATA #IMPLIED>
        <!ATTLIST game isbios (yes|no) string.Emptynostring.Empty>
        <!ATTLIST game cloneof CDATA #IMPLIED>
        <!ATTLIST game romof CDATA #IMPLIED>
        <!ATTLIST game sampleof CDATA #IMPLIED>
        <!ATTLIST game board CDATA #IMPLIED>
        <!ATTLIST game rebuildto CDATA #IMPLIED>
        <!ELEMENT year (#PCDATA)>
        <!ELEMENT manufacturer (#PCDATA)>
        <!ELEMENT release EMPTY>
            <!ATTLIST release name CDATA #REQUIRED>
            <!ATTLIST release region CDATA #REQUIRED>
            <!ATTLIST release language CDATA #IMPLIED>
            <!ATTLIST release date CDATA #IMPLIED>
            <!ATTLIST release default (yes|no) string.Emptynostring.Empty>
        <!ELEMENT biosset EMPTY>
            <!ATTLIST biosset name CDATA #REQUIRED>
            <!ATTLIST biosset description CDATA #REQUIRED>
            <!ATTLIST biosset default (yes|no) string.Emptynostring.Empty>
        <!ELEMENT rom EMPTY>
            <!ATTLIST rom name CDATA #REQUIRED>
            <!ATTLIST rom size CDATA #REQUIRED>
            <!ATTLIST rom crc CDATA #IMPLIED>
            <!ATTLIST rom md5 CDATA #IMPLIED>
            <!ATTLIST rom sha1 CDATA #IMPLIED>
            <!ATTLIST rom sha256 CDATA #IMPLIED>
            <!ATTLIST rom sha384 CDATA #IMPLIED>
            <!ATTLIST rom sha512 CDATA #IMPLIED>
            <!ATTLIST rom merge CDATA #IMPLIED>
            <!ATTLIST rom status (baddump|nodump|good|verified) string.Emptygoodstring.Empty>
            <!ATTLIST rom date CDATA #IMPLIED>
        <!ELEMENT disk EMPTY>
            <!ATTLIST disk name CDATA #REQUIRED>
            <!ATTLIST disk md5 CDATA #IMPLIED>
            <!ATTLIST disk sha1 CDATA #IMPLIED>
            <!ATTLIST disk merge CDATA #IMPLIED>
            <!ATTLIST disk status (baddump|nodump|good|verified) string.Emptygoodstring.Empty>
        <!ELEMENT sample EMPTY>
            <!ATTLIST sample name CDATA #REQUIRED>
        <!ELEMENT archive EMPTY>
            <!ATTLIST archive name CDATA #REQUIRED>
    <!ELEMENT machine (comment*, description, year?, manufacturer?, release*, biosset*, rom*, disk*, sample*, archive*)>
        <!ATTLIST machine name CDATA #REQUIRED>
        <!ATTLIST machine sourcefile CDATA #IMPLIED>
        <!ATTLIST machine isbios (yes|no) string.Emptynostring.Empty>
        <!ATTLIST machine cloneof CDATA #IMPLIED>
        <!ATTLIST machine romof CDATA #IMPLIED>
        <!ATTLIST machine sampleof CDATA #IMPLIED>
        <!ATTLIST machine board CDATA #IMPLIED>
        <!ATTLIST machine rebuildto CDATA #IMPLIED>
";

        /// <summary>
        /// Constructor designed for casting a base DatFile
        /// </summary>
        /// <param name="datFile">Parent DatFile to copy from</param>
        /// <param name="deprecated">True if the output uses "game", false if the output uses "machine"</param>
        public Logiqx(DatFile datFile, bool deprecated)
            : base(datFile)
        {
            _deprecated = deprecated;
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

            List<string> dirs = new List<string>();

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
                        // If we're ending a dir, remove the last item from the dirs list, if possible
                        if (xtr.Name == "dir" && dirs.Count > 0)
                            dirs.RemoveAt(dirs.Count - 1);

                        xtr.Read();
                        continue;
                    }

                    switch (xtr.Name)
                    {
                        // The datafile tag can have some attributes
                        case "datafile":
                            Header.Build ??= xtr.GetAttribute("build");
                            Header.Debug ??= xtr.GetAttribute("debug").AsYesNo();
                            xtr.Read();
                            break;

                        // We want to process the entire subtree of the header
                        case "header":
                            ReadHeader(xtr.ReadSubtree(), keep);

                            // Skip the header node now that we've processed it
                            xtr.Skip();
                            break;

                        // Unique to RomVault-created DATs
                        case "dir":
                            Header.Type = "SuperDAT";
                            dirs.Add(xtr.GetAttribute("name") ?? string.Empty);
                            xtr.Read();
                            break;

                        // We want to process the entire subtree of the game
                        case "machine": // New-style Logiqx
                        case "game": // Old-style Logiqx
                            ReadMachine(xtr.ReadSubtree(), dirs, statsOnly, filename, indexId, keep);

                            // Skip the machine now that we've processed it
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
        /// Read header information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the header</param>
        /// <param name="keep">True if full pathnames are to be kept, false otherwise (default)</param>
        private void ReadHeader(XmlReader reader, bool keep)
        {
            bool superdat = false;

            // If there's no subtree to the header, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element || reader.Name == "header")
                {
                    reader.Read();
                    continue;
                }

                // Get all header items (ONLY OVERWRITE IF THERE'S NO DATA)
                string content;
                switch (reader.Name)
                {
                    case "name":
                        content = reader.ReadElementContentAsString();
                        Header.Name ??= content;
                        superdat |= content.Contains(" - SuperDAT");
                        if (keep && superdat)
                        {
                            Header.Type ??= "SuperDAT";
                        }
                        break;

                    case "description":
                        content = reader.ReadElementContentAsString();
                        Header.Description ??= content;
                        break;

                    case "rootdir": // This is exclusive to TruRip XML
                        content = reader.ReadElementContentAsString();
                        Header.RootDir ??= content;
                        break;

                    case "category":
                        content = reader.ReadElementContentAsString();
                        Header.Category ??= content;
                        break;

                    case "version":
                        content = reader.ReadElementContentAsString();
                        Header.Version ??= content;
                        break;

                    case "date":
                        content = reader.ReadElementContentAsString();
                        Header.Date ??= content.Replace(".", "/");
                        break;

                    case "author":
                        content = reader.ReadElementContentAsString();
                        Header.Author ??= content;
                        break;

                    case "email":
                        content = reader.ReadElementContentAsString();
                        Header.Email ??= content;
                        break;

                    case "homepage":
                        content = reader.ReadElementContentAsString();
                        Header.Homepage ??= content;
                        break;

                    case "url":
                        content = reader.ReadElementContentAsString();
                        Header.Url ??= content;
                        break;

                    case "comment":
                        content = reader.ReadElementContentAsString();
                        Header.Comment = (Header.Comment ?? content);
                        break;

                    case "type": // This is exclusive to TruRip XML
                        content = reader.ReadElementContentAsString();
                        Header.Type ??= content;
                        superdat |= content.Contains("SuperDAT");
                        break;

                    case "clrmamepro":
                        if (Header.HeaderSkipper == null)
                            Header.HeaderSkipper = reader.GetAttribute("header");

                        if (Header.ForceMerging == MergingFlag.None)
                            Header.ForceMerging = reader.GetAttribute("forcemerging").AsMergingFlag();

                        if (Header.ForceNodump == NodumpFlag.None)
                            Header.ForceNodump = reader.GetAttribute("forcenodump").AsNodumpFlag();

                        if (Header.ForcePacking == PackingFlag.None)
                            Header.ForcePacking = reader.GetAttribute("forcepacking").AsPackingFlag();

                        reader.Read();
                        break;

                    case "romcenter":
                        if (Header.System == null)
                            Header.System = reader.GetAttribute("plugin");

                        if (Header.RomMode == MergingFlag.None)
                            Header.RomMode = reader.GetAttribute("rommode").AsMergingFlag();

                        if (Header.BiosMode == MergingFlag.None)
                            Header.BiosMode = reader.GetAttribute("biosmode").AsMergingFlag();

                        if (Header.SampleMode == MergingFlag.None)
                            Header.SampleMode = reader.GetAttribute("samplemode").AsMergingFlag();

                        if (Header.LockRomMode == null)
                            Header.LockRomMode = reader.GetAttribute("lockrommode").AsYesNo();

                        if (Header.LockBiosMode == null)
                            Header.LockBiosMode = reader.GetAttribute("lockbiosmode").AsYesNo();

                        if (Header.LockSampleMode == null)
                            Header.LockSampleMode = reader.GetAttribute("locksamplemode").AsYesNo();

                        reader.Read();
                        break;
                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <summary>
        /// Read game/machine information
        /// </summary>
        /// <param name="reader">XmlReader to use to parse the machine</param>
        /// <param name="dirs">List of dirs to prepend to the game name</param>
        /// <param name="statsOnly">True to only add item statistics while parsing, false otherwise</param>
        /// <param name="filename">Name of the file to be parsed</param>
        /// <param name="indexId">Index ID for the DAT</param>
        /// <param name="keep">True if full pathnames are to be kept, false otherwise (default)</param>
        private void ReadMachine(
            XmlReader reader,
            List<string> dirs,
            bool statsOnly,

            // Standard Dat parsing
            string filename,
            int indexId,

            // Miscellaneous
            bool keep)
        {
            // If we have an empty machine, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            bool containsItems = false;

            // Create a new machine
            MachineType machineType = 0x0;
            if (reader.GetAttribute("isbios").AsYesNo() == true)
                machineType |= MachineType.Bios;

            if (reader.GetAttribute("isdevice").AsYesNo() == true) // Listxml-specific, used by older DATs
                machineType |= MachineType.Device;

            if (reader.GetAttribute("ismechanical").AsYesNo() == true) // Listxml-specific, used by older DATs
                machineType |= MachineType.Mechanical;

            string dirsString = (dirs != null && dirs.Count() > 0 ? string.Join("/", dirs) + "/" : string.Empty);
            Machine machine = new Machine
            {
                Name = dirsString + reader.GetAttribute("name"),
                Description = dirsString + reader.GetAttribute("name"),
                SourceFile = reader.GetAttribute("sourcefile"),
                Board = reader.GetAttribute("board"),
                RebuildTo = reader.GetAttribute("rebuildto"),
                Runnable = reader.GetAttribute("runnable").AsRunnable(), // Used by older DATs

                CloneOf = reader.GetAttribute("cloneof"),
                RomOf = reader.GetAttribute("romof"),
                SampleOf = reader.GetAttribute("sampleof"),

                MachineType = (machineType == 0x0 ? MachineType.NULL : machineType),
            };

            if (Header.Type == "SuperDAT" && !keep)
            {
                string tempout = Regex.Match(machine.Name, @".*?\\(.*)").Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(tempout))
                    machine.Name = tempout;
            }

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
                    case "comment": // There can be multiple comments by spec
                        machine.Comment += reader.ReadElementContentAsString();
                        break;

                    case "description":
                        machine.Description = reader.ReadElementContentAsString();
                        break;

                    case "year":
                        machine.Year = reader.ReadElementContentAsString();
                        break;

                    case "manufacturer":
                        machine.Manufacturer = reader.ReadElementContentAsString();
                        break;

                    case "publisher": // Not technically supported but used by some legacy DATs
                        machine.Publisher = reader.ReadElementContentAsString();
                        break;

                    case "category": // Not technically supported but used by some legacy DATs
                        machine.Category = reader.ReadElementContentAsString();
                        break;

                    case "trurip": // This is special metadata unique to EmuArc
                        ReadTruRip(reader.ReadSubtree(), machine);

                        // Skip the trurip node now that we've processed it
                        reader.Skip();
                        break;

                    case "archive":
                        containsItems = true;

                        DatItem archive = new Archive
                        {
                            Name = reader.GetAttribute("name"),

                            Source = new Source
                            {
                                Index = indexId,
                                Name = filename,
                            },
                        };

                        archive.CopyMachineInformation(machine);

                        // Now process and add the archive
                        ParseAddHelper(archive, statsOnly);

                        reader.Read();
                        break;

                    case "biosset":
                        containsItems = true;

                        DatItem biosSet = new BiosSet
                        {
                            Name = reader.GetAttribute("name"),
                            Description = reader.GetAttribute("description"),
                            Default = reader.GetAttribute("default").AsYesNo(),

                            Source = new Source
                            {
                                Index = indexId,
                                Name = filename,
                            },
                        };

                        biosSet.CopyMachineInformation(machine);

                        // Now process and add the biosSet
                        ParseAddHelper(biosSet, statsOnly);

                        reader.Read();
                        break;

                    case "disk":
                        containsItems = true;

                        DatItem disk = new Disk
                        {
                            Name = reader.GetAttribute("name"),
                            MD5 = reader.GetAttribute("md5"),
                            SHA1 = reader.GetAttribute("sha1"),
                            MergeTag = reader.GetAttribute("merge"),
                            ItemStatus = reader.GetAttribute("status").AsItemStatus(),

                            Source = new Source
                            {
                                Index = indexId,
                                Name = filename,
                            },
                        };

                        disk.CopyMachineInformation(machine);

                        // Now process and add the disk
                        ParseAddHelper(disk, statsOnly);

                        reader.Read();
                        break;

                    case "media":
                        containsItems = true;

                        DatItem media = new Media
                        {
                            Name = reader.GetAttribute("name"),
                            MD5 = reader.GetAttribute("md5"),
                            SHA1 = reader.GetAttribute("sha1"),
                            SHA256 = reader.GetAttribute("sha256"),
                            SpamSum = reader.GetAttribute("spamsum"),

                            Source = new Source
                            {
                                Index = indexId,
                                Name = filename,
                            },
                        };

                        media.CopyMachineInformation(machine);

                        // Now process and add the media
                        ParseAddHelper(media, statsOnly);

                        reader.Read();
                        break;

                    case "release":
                        containsItems = true;

                        DatItem release = new Release
                        {
                            Name = reader.GetAttribute("name"),
                            Region = reader.GetAttribute("region"),
                            Language = reader.GetAttribute("language"),
                            Date = reader.GetAttribute("date"),
                            Default = reader.GetAttribute("default").AsYesNo(),
                        };

                        release.CopyMachineInformation(machine);

                        // Now process and add the release
                        ParseAddHelper(release, statsOnly);

                        reader.Read();
                        break;

                    case "rom":
                        containsItems = true;

                        DatItem rom = new Rom
                        {
                            Name = reader.GetAttribute("name"),
                            Size = Utilities.CleanLong(reader.GetAttribute("size")),
                            CRC = reader.GetAttribute("crc"),
                            MD5 = reader.GetAttribute("md5"),
                            SHA1 = reader.GetAttribute("sha1"),
                            SHA256 = reader.GetAttribute("sha256"),
                            SHA384 = reader.GetAttribute("sha384"),
                            SHA512 = reader.GetAttribute("sha512"),
                            SpamSum = reader.GetAttribute("spamsum"),
                            MergeTag = reader.GetAttribute("merge"),
                            ItemStatus = reader.GetAttribute("status").AsItemStatus(),
                            Date = CleanDate(reader.GetAttribute("date")),
                            Inverted = reader.GetAttribute("inverted").AsYesNo(),

                            Source = new Source
                            {
                                Index = indexId,
                                Name = filename,
                            },
                        };

                        rom.CopyMachineInformation(machine);

                        // Now process and add the rom
                        ParseAddHelper(rom, statsOnly);

                        reader.Read();
                        break;

                    case "sample":
                        containsItems = true;

                        DatItem sample = new Sample
                        {
                            Name = reader.GetAttribute("name"),

                            Source = new Source
                            {
                                Index = indexId,
                                Name = filename,
                            },
                        };

                        sample.CopyMachineInformation(machine);

                        // Now process and add the sample
                        ParseAddHelper(sample, statsOnly);

                        reader.Read();
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
        /// Read EmuArc information
        /// </summary>
        /// <param name="keep">True if full pathnames are to be kept, false otherwise (default)</param>
        /// <param name="machine">Machine information to pass to contained items</param>
        private void ReadTruRip(XmlReader reader, Machine machine)
        {
            // If we have an empty trurip, skip it
            if (reader == null)
                return;

            // Otherwise, add what is possible
            reader.MoveToContent();

            while (!reader.EOF)
            {
                // We only want elements
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Read();
                    continue;
                }

                // Get the information from the trurip
                switch (reader.Name)
                {
                    case "titleid":
                        machine.TitleID = reader.ReadElementContentAsString();
                        break;

                    case "publisher":
                        machine.Publisher = reader.ReadElementContentAsString();
                        break;

                    case "developer":
                        machine.Developer = reader.ReadElementContentAsString();
                        break;

                    case "year":
                        machine.Year = reader.ReadElementContentAsString();
                        break;

                    case "genre":
                        machine.Genre = reader.ReadElementContentAsString();
                        break;

                    case "subgenre":
                        machine.Subgenre = reader.ReadElementContentAsString();
                        break;

                    case "ratings":
                        machine.Ratings = reader.ReadElementContentAsString();
                        break;

                    case "score":
                        machine.Score = reader.ReadElementContentAsString();
                        break;

                    case "players":
                        machine.Players = reader.ReadElementContentAsString();
                        break;

                    case "enabled":
                        machine.Enabled = reader.ReadElementContentAsString();
                        break;

                    case "crc":
                        machine.Crc = reader.ReadElementContentAsString().AsYesNo();
                        break;

                    case "source":
                        machine.SourceFile = reader.ReadElementContentAsString();
                        break;

                    case "cloneof":
                        machine.CloneOf = reader.ReadElementContentAsString();
                        break;

                    case "relatedto":
                        machine.RelatedTo = reader.ReadElementContentAsString();
                        break;

                    default:
                        reader.Read();
                        break;
                }
            }
        }

        /// <inheritdoc/>
        protected override ItemType[] GetSupportedTypes()
        {
            return new ItemType[]
            {
                ItemType.Archive,
                ItemType.BiosSet,
                ItemType.Disk,
                ItemType.Media,
                ItemType.Release,
                ItemType.Rom,
                ItemType.Sample,
            };
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
            xtw.WriteDocType("datafile", "-//Logiqx//DTD ROM Management Datafile//EN", "http://www.logiqx.com/Dats/datafile.dtd", null);

            xtw.WriteStartElement("datafile");
            xtw.WriteOptionalAttributeString("build", Header.Build);
            xtw.WriteOptionalAttributeString("debug", Header.Debug.FromYesNo());

            xtw.WriteStartElement("header");

            xtw.WriteRequiredElementString("name", Header.Name);
            xtw.WriteRequiredElementString("description", Header.Description);
            xtw.WriteOptionalElementString("rootdir", Header.RootDir);
            xtw.WriteOptionalElementString("category", Header.Category);
            xtw.WriteRequiredElementString("version", Header.Version);
            xtw.WriteOptionalElementString("date", Header.Date);
            xtw.WriteRequiredElementString("author", Header.Author);
            xtw.WriteOptionalElementString("email", Header.Email);
            xtw.WriteOptionalElementString("homepage", Header.Homepage);
            xtw.WriteOptionalElementString("url", Header.Url);
            xtw.WriteOptionalElementString("comment", Header.Comment);
            xtw.WriteOptionalElementString("type", Header.Type);

            if (Header.ForcePacking != PackingFlag.None
                || Header.ForceMerging != MergingFlag.None
                || Header.ForceNodump != NodumpFlag.None
                || !string.IsNullOrWhiteSpace(Header.HeaderSkipper))
            {
                xtw.WriteStartElement("clrmamepro");

                xtw.WriteOptionalAttributeString("forcepacking", Header.ForcePacking.FromPackingFlag(false));
                xtw.WriteOptionalAttributeString("forcemerging", Header.ForceMerging.FromMergingFlag(false));
                xtw.WriteOptionalAttributeString("forcenodump", Header.ForceNodump.FromNodumpFlag());
                xtw.WriteOptionalAttributeString("header", Header.HeaderSkipper);

                // End clrmamepro
                xtw.WriteEndElement();
            }

            if (Header.System != null
                || Header.RomMode != MergingFlag.None || Header.LockRomMode != null
                || Header.BiosMode != MergingFlag.None || Header.LockBiosMode != null
                || Header.SampleMode != MergingFlag.None || Header.LockSampleMode != null)
            {
                xtw.WriteStartElement("romcenter");

                xtw.WriteOptionalAttributeString("plugin", Header.System);
                xtw.WriteOptionalAttributeString("rommode", Header.RomMode.FromMergingFlag(true));
                xtw.WriteOptionalAttributeString("biosmode", Header.BiosMode.FromMergingFlag(true));
                xtw.WriteOptionalAttributeString("samplemode", Header.SampleMode.FromMergingFlag(true));
                xtw.WriteOptionalAttributeString("lockrommode", Header.LockRomMode.FromYesNo());
                xtw.WriteOptionalAttributeString("lockbiosmode", Header.LockBiosMode.FromYesNo());
                xtw.WriteOptionalAttributeString("locksamplemode", Header.LockSampleMode.FromYesNo());

                // End romcenter
                xtw.WriteEndElement();
            }

            // End header
            xtw.WriteEndElement();

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
            xtw.WriteStartElement(_deprecated ? "game" : "machine");
            xtw.WriteRequiredAttributeString("name", datItem.Machine.Name);

            if (datItem.Machine.MachineType.HasFlag(MachineType.Bios))
                xtw.WriteAttributeString("isbios", "yes");
            if (datItem.Machine.MachineType.HasFlag(MachineType.Device))
                xtw.WriteAttributeString("isdevice", "yes");
            if (datItem.Machine.MachineType.HasFlag(MachineType.Mechanical))
                xtw.WriteAttributeString("ismechanical", "yes");

            xtw.WriteOptionalAttributeString("runnable", datItem.Machine.Runnable.FromRunnable());

            if (!string.Equals(datItem.Machine.Name, datItem.Machine.CloneOf, StringComparison.OrdinalIgnoreCase))
                xtw.WriteOptionalAttributeString("cloneof", datItem.Machine.CloneOf);
            if (!string.Equals(datItem.Machine.Name, datItem.Machine.RomOf, StringComparison.OrdinalIgnoreCase))
                xtw.WriteOptionalAttributeString("romof", datItem.Machine.RomOf);
            if (!string.Equals(datItem.Machine.Name, datItem.Machine.SampleOf, StringComparison.OrdinalIgnoreCase))
                xtw.WriteOptionalAttributeString("sampleof", datItem.Machine.SampleOf);

            xtw.WriteOptionalElementString("comment", datItem.Machine.Comment);
            xtw.WriteOptionalElementString("description", datItem.Machine.Description);
            xtw.WriteOptionalElementString("year", datItem.Machine.Year);
            xtw.WriteOptionalElementString("publisher", datItem.Machine.Publisher);
            xtw.WriteOptionalElementString("manufacturer", datItem.Machine.Manufacturer);
            xtw.WriteOptionalElementString("category", datItem.Machine.Category);

            if (datItem.Machine.TitleID != null
                || datItem.Machine.Developer != null
                || datItem.Machine.Genre != null
                || datItem.Machine.Subgenre != null
                || datItem.Machine.Ratings != null
                || datItem.Machine.Score != null
                || datItem.Machine.Enabled != null
                || datItem.Machine.Crc != null
                || datItem.Machine.RelatedTo != null)
            {
                xtw.WriteStartElement("trurip");

                xtw.WriteOptionalElementString("titleid", datItem.Machine.TitleID);
                xtw.WriteOptionalElementString("publisher", datItem.Machine.Publisher);
                xtw.WriteOptionalElementString("developer", datItem.Machine.Developer);
                xtw.WriteOptionalElementString("year", datItem.Machine.Year);
                xtw.WriteOptionalElementString("genre", datItem.Machine.Genre);
                xtw.WriteOptionalElementString("subgenre", datItem.Machine.Subgenre);
                xtw.WriteOptionalElementString("ratings", datItem.Machine.Ratings);
                xtw.WriteOptionalElementString("score", datItem.Machine.Score);
                xtw.WriteOptionalElementString("players", datItem.Machine.Players);
                xtw.WriteOptionalElementString("enabled", datItem.Machine.Enabled);
                xtw.WriteOptionalElementString("titleid", datItem.Machine.TitleID);
                xtw.WriteOptionalElementString("crc", datItem.Machine.Crc.FromYesNo());
                xtw.WriteOptionalElementString("source", datItem.Machine.SourceFile);
                xtw.WriteOptionalElementString("cloneof", datItem.Machine.CloneOf);
                xtw.WriteOptionalElementString("relatedto", datItem.Machine.RelatedTo);

                // End trurip
                xtw.WriteEndElement();
            }

            xtw.Flush();
        }

        /// <summary>
        /// Write out Game end using the supplied StreamWriter
        /// </summary>
        /// <param name="xtw">XmlTextWriter to output to</param>
        private void WriteEndGame(XmlTextWriter xtw)
        {
            // End machine
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
                case ItemType.Archive:
                    var archive = datItem as Archive;
                    xtw.WriteStartElement("archive");
                    xtw.WriteRequiredAttributeString("name", archive.Name);
                    xtw.WriteEndElement();
                    break;

                case ItemType.BiosSet:
                    var biosSet = datItem as BiosSet;
                    xtw.WriteStartElement("biosset");
                    xtw.WriteRequiredAttributeString("name", biosSet.Name);
                    xtw.WriteOptionalAttributeString("description", biosSet.Description);
                    xtw.WriteOptionalAttributeString("default", biosSet.Default.FromYesNo());
                    xtw.WriteEndElement();
                    break;

                case ItemType.Disk:
                    var disk = datItem as Disk;
                    xtw.WriteStartElement("disk");
                    xtw.WriteRequiredAttributeString("name", disk.Name);
                    xtw.WriteOptionalAttributeString("md5", disk.MD5?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha1", disk.SHA1?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("status", disk.ItemStatus.FromItemStatus(false));
                    xtw.WriteEndElement();
                    break;

                case ItemType.Media:
                    var media = datItem as Media;
                    xtw.WriteStartElement("media");
                    xtw.WriteRequiredAttributeString("name", media.Name);
                    xtw.WriteOptionalAttributeString("md5", media.MD5?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha1", media.SHA1?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha256", media.SHA256?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("spamsum", media.SpamSum?.ToLowerInvariant());
                    xtw.WriteEndElement();
                    break;

                case ItemType.Release:
                    var release = datItem as Release;
                    xtw.WriteStartElement("release");
                    xtw.WriteRequiredAttributeString("name", release.Name);
                    xtw.WriteOptionalAttributeString("region", release.Region);
                    xtw.WriteOptionalAttributeString("language", release.Language);
                    xtw.WriteOptionalAttributeString("date", release.Date);
                    xtw.WriteOptionalAttributeString("default", release.Default.FromYesNo());
                    xtw.WriteEndElement();
                    break;

                case ItemType.Rom:
                    var rom = datItem as Rom;
                    xtw.WriteStartElement("rom");
                    xtw.WriteRequiredAttributeString("name", rom.Name);
                    xtw.WriteAttributeString("size", rom.Size?.ToString());
                    xtw.WriteOptionalAttributeString("crc", rom.CRC?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("md5", rom.MD5?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha1", rom.SHA1?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha256", rom.SHA256?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha384", rom.SHA384?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("sha512", rom.SHA512?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("spamsum", rom.SpamSum?.ToLowerInvariant());
                    xtw.WriteOptionalAttributeString("date", rom.Date);
                    xtw.WriteOptionalAttributeString("status", rom.ItemStatus.FromItemStatus(false));
                    xtw.WriteOptionalAttributeString("inverted", rom.Inverted.FromYesNo());
                    xtw.WriteEndElement();
                    break;

                case ItemType.Sample:
                    var sample = datItem as Sample;
                    xtw.WriteStartElement("sample");
                    xtw.WriteRequiredAttributeString("name", sample.Name);
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
            // End machine
            xtw.WriteEndElement();

            // End datafile
            xtw.WriteEndElement();

            xtw.Flush();
        }
    }
}
