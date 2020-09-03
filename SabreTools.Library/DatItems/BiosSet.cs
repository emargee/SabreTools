﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

using SabreTools.Library.Filtering;
using SabreTools.Library.Tools;
using Newtonsoft.Json;

namespace SabreTools.Library.DatItems
{
    /// <summary>
    /// Represents which BIOS(es) is associated with a set
    /// </summary>
    [JsonObject("biosset")]
    public class BiosSet : DatItem
    {
        #region Fields

        /// <summary>
        /// Name of the item
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Description of the BIOS
        /// </summary>
        [JsonProperty("description", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Description { get; set; }

        /// <summary>
        /// Determine whether the BIOS is default
        /// </summary>
        [JsonProperty("default", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? Default { get; set; }

        #endregion

        #region Accessors

        /// <summary>
        /// Gets the name to use for a DatItem
        /// </summary>
        /// <returns>Name if available, null otherwise</returns>
        public override string GetName()
        {
            return Name;
        }

        /// <summary>
        /// Set fields with given values
        /// </summary>
        /// <param name="mappings">Mappings dictionary</param>
        public override void SetFields(Dictionary<Field, string> mappings)
        {
            // Set base fields
            base.SetFields(mappings);

            // Handle BiosSet-specific fields
            if (mappings.Keys.Contains(Field.DatItem_Name))
                Name = mappings[Field.DatItem_Name];

            if (mappings.Keys.Contains(Field.DatItem_Default))
                Default = mappings[Field.DatItem_Default].AsYesNo();

            if (mappings.Keys.Contains(Field.DatItem_Description))
                Description = mappings[Field.DatItem_Description];
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Create a default, empty BiosSet object
        /// </summary>
        public BiosSet()
        {
            Name = string.Empty;
            ItemType = ItemType.BiosSet;
        }

        #endregion

        #region Cloning Methods

        public override object Clone()
        {
            return new BiosSet()
            {
                Name = this.Name,
                ItemType = this.ItemType,
                DupeType = this.DupeType,

                AltName = this.AltName,
                AltTitle = this.AltTitle,

                Original = this.Original,
                OpenMSXSubType = this.OpenMSXSubType,
                OpenMSXType = this.OpenMSXType,
                Remark = this.Remark,
                Boot = this.Boot,

                LoadFlag = this.LoadFlag,

                Machine = this.Machine.Clone() as Machine,
                Source = this.Source.Clone() as Source,
                Remove = this.Remove,

                Description = this.Description,
                Default = this.Default,
            };
        }

        #endregion

        #region Comparision Methods

        public override bool Equals(DatItem other)
        {
            // If we don't have a BiosSet, return false
            if (ItemType != other.ItemType)
                return false;

            // Otherwise, treat it as a BiosSet
            BiosSet newOther = other as BiosSet;

            // If the BiosSet information matches
            return (Name == newOther.Name && Description == newOther.Description && Default == newOther.Default);
        }

        #endregion

        #region Filtering

        /// <summary>
        /// Clean a DatItem according to the cleaner
        /// </summary>
        /// <param name="cleaner">Cleaner to implement</param>
        public override void Clean(Cleaner cleaner)
        {
            // Clean common items first
            base.Clean(cleaner);

            // If we're stripping unicode characters, strip item name
            if (cleaner?.RemoveUnicode == true)
                Name = Sanitizer.RemoveUnicodeCharacters(Name);

            // If we are in NTFS trim mode, trim the game name
            if (cleaner?.Trim == true)
            {
                // Windows max name length is 260
                int usableLength = 260 - Machine.Name.Length - (cleaner.Root?.Length ?? 0);
                if (Name.Length > usableLength)
                {
                    string ext = Path.GetExtension(Name);
                    Name = Name.Substring(0, usableLength - ext.Length);
                    Name += ext;
                }
            }
        }

        /// <summary>
        /// Check to see if a DatItem passes the filter
        /// </summary>
        /// <param name="filter">Filter to check against</param>
        /// <returns>True if the item passed the filter, false otherwise</returns>
        public override bool PassesFilter(Filter filter)
        {
            // Check common fields first
            if (!base.PassesFilter(filter))
                return false;

            // Filter on item name
            if (filter.DatItem_Name.MatchesPositiveSet(Name) == false)
                return false;
            if (filter.DatItem_Name.MatchesNegativeSet(Name) == true)
                return false;

            // Filter on description
            if (filter.DatItem_Description.MatchesPositiveSet(Description) == false)
                return false;
            if (filter.DatItem_Description.MatchesNegativeSet(Description) == true)
                return false;

            // Filter on default
            if (filter.DatItem_Default.MatchesNeutral(null, Default) == false)
                return false;

            return true;
        }

        /// <summary>
        /// Remove fields from the DatItem
        /// </summary>
        /// <param name="fields">List of Fields to remove</param>
        public override void RemoveFields(List<Field> fields)
        {
            // Remove common fields first
            base.RemoveFields(fields);

            // Remove the fields
            if (fields.Contains(Field.DatItem_Name))
                Name = null;

            if (fields.Contains(Field.DatItem_Description))
                Description = null;

            if (fields.Contains(Field.DatItem_Default))
                Default = null;
        }

        /// <summary>
        /// Set internal names to match One Rom Per Game (ORPG) logic
        /// </summary>
        public override void SetOneRomPerGame()
        {
            string[] splitname = Name.Split('.');
            Machine.Name += $"/{string.Join(".", splitname.Take(splitname.Length > 1 ? splitname.Length - 1 : 1))}";
            Name = Path.GetFileName(Name);
        }

        #endregion

        #region Sorting and Merging

        /// <summary>
        /// Replace fields from another item
        /// </summary>
        /// <param name="item">DatItem to pull new information from</param>
        /// <param name="fields">List of Fields representing what should be updated</param>
        public override void ReplaceFields(DatItem item, List<Field> fields)
        {
            // Replace common fields first
            base.ReplaceFields(item, fields);

            // If we don't have a BiosSet to replace from, ignore specific fields
            if (item.ItemType != ItemType.BiosSet)
                return;

            // Cast for easier access
            BiosSet newItem = item as BiosSet;

            // Replace the fields
            if (fields.Contains(Field.DatItem_Name))
                Name = newItem.Name;

            if (fields.Contains(Field.DatItem_Description))
                Description = newItem.Description;

            if (fields.Contains(Field.DatItem_Default))
                Default = newItem.Default;
        }

        #endregion
    }
}
