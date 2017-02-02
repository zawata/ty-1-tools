﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ty_mod_manager
{
    public class TyMod
    {
        #region Private Definitions

        private struct ModTag
        {
            public string Name;
            public ModTagAdd Add;
        }

        private delegate void ModTagAdd(TyMod tymod, XmlNode node);

        private ModTag[] _modTags =
        {
            new ModTag() { Name = "global",  Add = new ModTagAdd(AddFromNode_Global) },
            new ModTag() { Name = "resource",  Add = new ModTagAdd(AddFromNode_Resource) },
            new ModTag() { Name = "translation", Add = new ModTagAdd(AddFromNode_Translation) },
            new ModTag() { Name = "plugin",  Add = new ModTagAdd(AddFromNode_Plugin) },
            new ModTag() { Name = "level", Add = new ModTagAdd(AddFromNode_Level) }
        };

        #endregion

        #region Public Definitions

        public struct TyModImport
        {
            public bool Plugin;
            public string Source;
            public string Destination;
        }

        #endregion

        #region Properties

        private bool _valid = false;

        public string Name { get; } = null;
        public string Authors { get; } = null;
        public string Description { get; } = null;
        public string ModVersion { get; } = null;
        public TyVersionRange VersionRange { get; } = null;
        public List<TyModEdit> Edits { get; } = null;
        public List<TyModImport> Imports { get; } = null;
        public List<TyModTranslate> Translations { get; } = null;
        public List<TyLevel> Levels { get; } = null;
        public bool Valid { get { return _valid; } }

        #endregion

        public TyMod(string name, TyVersionRange versionRange = null, string version = null, string authors = null, string description = null)
        {
            Name = name;
            VersionRange = versionRange != null && versionRange.Valid ? versionRange : null;
            ModVersion = version;
            Authors = authors ?? String.Empty;
            Description = description ?? String.Empty;

            Edits = new List<TyModEdit>();
            Imports = new List<TyModImport>();
            Translations = new List<TyModTranslate>();
            Levels = new List<TyLevel>();
        }

        public override string ToString()
        {
            return base.ToString();
        }

        public void AddFromNode(XmlNode node)
        {
            ModTag tag;

            for (int x = 0; x < node.ChildNodes.Count; x++)
            {
                tag = _modTags.Where(w => w.Name == node.ChildNodes[x].Name).FirstOrDefault();
                tag.Add?.Invoke(this, node.ChildNodes[x]);
            }

            if (Edits.Count > 0 || Imports.Count > 0 || Translations.Count > 0 || Levels.Count > 0)
                _valid = true;
        }

        #region Add Mod From XmlNode

        private static void AddFromNode_Global(TyMod tymod, XmlNode node)
        {
            string source, type, context = null;
            TyModEdit.EditType editType = TyModEdit.EditType.inclusive;
            TyModEdit modEdit;

            if (node.Attributes == null)
            {
                Program.Log("No attributes assigned to global \"" + node.OuterXml + "\"");
                return;
            }

            try { source = node.Attributes.GetNamedItem("source").Value; } catch (Exception e) { Program.Log("Invalid source attribute for global \"" + node.OuterXml + "\"", e); return; }
            try { type = node.Attributes.GetNamedItem("type").Value; editType = (TyModEdit.EditType)Enum.Parse(typeof(TyModEdit.EditType), type); } catch { }
            try { context = node.Attributes.GetNamedItem("context").Value.ToLower(); } catch { }

            modEdit = new TyModEdit(source, editType, context);

            foreach (XmlAttribute attr in node.Attributes)
            {
                if (attr.Name == "source" || attr.Name == "type" || attr.Name == "context")
                    continue;

                if (attr.Name == "value")
                    modEdit.Value = attr.Value;
                else
                    modEdit.Attributes.Add(attr.Name, attr.Value);
            }

            AddFromNode_Global_Recursive(modEdit, node);

            tymod.Edits.Add(modEdit);
        }

        private static void AddFromNode_Global_Recursive(TyModEdit modEdit, XmlNode node)
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                if (childNode.Attributes != null)
                    foreach (XmlAttribute attr in childNode.Attributes)
                        if (attr.Name == "value")
                            modEdit.SubItems.Add(new TyModEditItem(childNode.Name, attr.Value));

                AddFromNode_Global_Recursive(modEdit, childNode);
            }
        }

        private static void AddFromNode_Resource(TyMod tymod, XmlNode node)
        {
            string source, dest = null;

            try { source = node.Attributes.GetNamedItem("source").Value; } catch (Exception e) { Program.Log("Invalid source attribute for resource import \"" + node.OuterXml + "\"", e); return; }
            try { dest = node.Attributes.GetNamedItem("dest").Value; } catch { }

            tymod.Imports.Add(new TyModImport() { Source = source, Destination = dest, Plugin = false });
        }

        private static void AddFromNode_Translation(TyMod tymod, XmlNode node)
        {
            string name;
            TyModTranslate modTranslate;

            try { name = node.Attributes.GetNamedItem("name").Value; } catch (Exception e) { Program.Log("Invalid or missing name attribute for translation \"" + node.OuterXml + "\"", e); return; }

            modTranslate = new TyModTranslate(name);

            foreach (XmlNode child in node.ChildNodes)
            {
                name = child.Name.ToLower();
                foreach (XmlAttribute attr in child.Attributes)
                {
                    if (attr.Name != "value")
                        continue;

                    if (modTranslate.Translations.ContainsKey(name))
                        modTranslate.Translations[name] = attr.Value;
                    else
                        modTranslate.Translations.Add(name, attr.Value);
                }
            }

            tymod.Translations.Add(modTranslate);
        }

        private static void AddFromNode_Plugin(TyMod tymod, XmlNode node)
        {
            string source;

            try { source = node.Attributes.GetNamedItem("source").Value; } catch (Exception e) { Program.Log("Invalid source attribute for plugin import \"" + node.OuterXml + "\"", e); return; }

            tymod.Imports.Add(new TyModImport() { Source = source, Destination = null, Plugin = true });
        }

        private static void AddFromNode_Level(TyMod tymod, XmlNode node)
        {
            string directory = null;
            TyLevel level;

            try { directory = node.Attributes.GetNamedItem("directory").Value; } catch (Exception e) { Program.Log("Invalid directory attribute for level import \"" + node.OuterXml + "\"", e); return; }

            level = new TyLevel(Path.Combine(Program.ModDirectory, directory));
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "name")
                {
                    foreach (XmlNode grandchild in child.ChildNodes)
                    {
                        try
                        {
                            level.LanguageNames[grandchild.Name.ToLower()] = grandchild.Attributes.GetNamedItem("value").Value;
                        }
                        catch (Exception e) { Program.Log("Invalid directory attribute for level import \"" + node.OuterXml + "\"", e); return; }
                    }
                }
                else if (child.Name == "position")
                {
                    foreach (XmlNode grandchild in child.ChildNodes)
                    {
                        try
                        {
                            switch (grandchild.Name.ToLower())
                            {
                                case "x":
                                    level.X = float.Parse(grandchild.Attributes.GetNamedItem("value").Value);
                                    break;
                                case "y":
                                    level.Y = float.Parse(grandchild.Attributes.GetNamedItem("value").Value);
                                    break;
                                case "z":
                                    level.Z = float.Parse(grandchild.Attributes.GetNamedItem("value").Value);
                                    break;
                            }
                        }
                        catch (Exception e) { Program.Log("Invalid directory attribute for level import \"" + node.OuterXml + "\"", e); return; }
                    }
                }
            }

            // Add to list of levels
            tymod.Levels.Add(level);
        }

        #endregion

        #region Apply Mod

        public static void ApplyImport(TyModImport import)
        {
            string src, dst;
            FileInfo fi;

            if (import.Source == null || (!File.Exists((src = Path.Combine(Program.ModDirectory, import.Source))) && !Directory.Exists(src)))
            {
                Program.Log("Unable to find resource \"" + import.Source==null?"(null)":import.Source + "\"", null, true);
                return;
            }

            if (import.Destination == null)
                dst = Path.Combine(Program.OutDirectory, import.Source);
            else
                dst = Path.Combine(Program.OutDirectory, import.Destination);

            if (import.Plugin)
            {
                // to-do
            }
            else if (import.Destination != null && import.Destination != String.Empty)
            {
                fi = new FileInfo(dst);

                // Determine if copying folder or file
                if (!File.Exists(src) && fi.Name == "")
                {
                    // Delete any copy of the dst directory in the output folder unless the output folder is the dst directory
                    if (Path.GetFullPath(dst) != Path.GetFullPath(Program.OutDirectory) && fi.Directory.Exists)
                    {
                        Directory.Delete(fi.Directory.FullName, true);

                        // Wait until directory is deleted
                        while (Directory.Exists(Program.OutDirectory))
                            System.Threading.Thread.Sleep(10);
                    }

                    ApplyImport_Directory(src, fi.Directory.FullName);
                }
                else
                {
                    // Ensure the directory exists for copying
                    if (!fi.Directory.Exists)
                        Directory.CreateDirectory(fi.Directory.FullName);

                    // Delete any pre-existing copy
                    if (fi.Exists)
                        File.Delete(dst);

                    // Copy
                    File.Copy(src, dst);
                }
            }
        }

        private static void ApplyImport_Directory(string directory, string outputDir)
        {
            DirectoryInfo di;
            FileInfo fi;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            foreach (string path in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                di = new DirectoryInfo(path);
                ApplyImport_Directory(path, Path.Combine(outputDir, di.Name));
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                fi = new FileInfo(file);
                File.Copy(file, Path.Combine(outputDir, fi.Name));
            }
        }

        public static void ApplyTranslate(Dictionary<string, TyTranslation> translations, TyModTranslate edit)
        {
            foreach (string key in edit.Translations.Keys)
            {
                if (!translations.ContainsKey(key))
                    continue;

                string value = edit.Translations[key];
                TyTranslation trans = translations[key];

                if (trans.Translations.ContainsKey(edit.Name))
                    trans.Translations[edit.Name] = value;

                // No longer can support adding new translations
                // They could interfere with custom maps

                //else
                //    trans.Translations.Add(edit.Name, value);
            }
        }

        public static void ApplyLevel(string levelID, TyLevel level)
        {
            ApplyLevel_Directory(levelID, level, level.InputPath, Program.OutDirectory);
        }

        private static void ApplyLevel_Directory(string levelID, TyLevel level, string directory, string outputDir)
        {
            DirectoryInfo di;
            FileInfo fi;
            string outfile;

            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            foreach (string path in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                di = new DirectoryInfo(path);
                ApplyLevel_Directory(levelID, level, path, Path.Combine(outputDir, di.Name));
            }

            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                fi = new FileInfo(file);
                outfile = Path.Combine(outputDir, fi.Name.Replace("%l", levelID));

                switch (fi.Extension.ToLower())
                {
                    case ".lv2":
                    case ".ini":
                        // For these file types, we support replacing %s with the level ID
                        File.WriteAllText(outfile, File.ReadAllText(file).Replace("%l", levelID));
                        break;
                    default:
                        File.Copy(file, outfile);
                        break;
                }
            }
        }

        public static void ApplyGlobal(TyGlobal global, TyModEdit edit)
        {
            int count = 0;
            bool addNew = true;

            if (edit.Attributes.Count > 0)
            {
                // Find with TyGlobal based on first attribute
                string key = edit.Attributes.Keys.ElementAt(0);
                string regex = edit.Attributes[key];

                foreach (TyGlobalItem item in global.Items)
                    if (ApplyGlobal_Find(global, item, edit, key, regex, ref count) > 0)
                        addNew = false;
            }

            if (!addNew)
                return;

            // Add new to TyGlobal
            for (int x = 0; x < edit.SubItems.Count; x++)
            {
                global.Items.Add(new TyGlobalItem(edit.SubItems[x].Key, edit.SubItems[x].Value, (global.Items.Count == 0 ? null : global.Items[global.Items.Count - 1].Context), 0, false));
                ApplyGlobal_Add(global, global.Items[global.Items.Count - 1], edit.SubItems[x]);
            }

        }

        public static void ApplyGlobal_Add(TyGlobal global, TyGlobalItem root, TyModEditItem edit)
        {
            for (int x = 0; x < edit.SubItems.Count; x++)
                root.SubItems.Add(new TyGlobalItem(edit.SubItems[x].Key, edit.SubItems[x].Value, root.Context, root.Indents + 4, ApplyGlobal_EqualSign(global, root, edit)));
        }

        public static int ApplyGlobal_Find(TyGlobal global, TyGlobalItem item, TyModEdit edit, string key, string regex, ref int count)
        {
            if (item.Key != null && item.Key == key)
            {
                if (item.Value != null && Regex.IsMatch(item.Value, regex))
                {
                    if (edit.Context == null || (item.Context != null && Regex.IsMatch(item.Context, edit.Context, RegexOptions.Multiline)))
                    {
                        ApplyGlobal_Apply(global, item, edit);
                        count++;
                    }
                }
            }

            foreach (TyGlobalItem sub in item.SubItems)
                ApplyGlobal_Find(global, sub, edit, key, regex, ref count);

            return count;
        }

        public static void ApplyGlobal_Apply(TyGlobal global, TyGlobalItem item, TyModEdit edit)
        {
            TyGlobalItem newItem;

            // If we are directly setting the value
            if (edit.Value != null)
                item.Value = edit.Value;

            switch (edit.Type)
            {
                case TyModEdit.EditType.exclusive:
                    item.SubItems.Clear();
                    for (int x = 0; x < edit.SubItems.Count; x++)
                        item.SubItems.Add(new TyGlobalItem(edit.SubItems[x].Key, edit.SubItems[x].Value, item.Context, item.Indents + 4, ApplyGlobal_EqualSign(global, item, edit.SubItems[x])));
                    break;
                case TyModEdit.EditType.inclusive:

                    for (int x = 0; x < edit.SubItems.Count; x++)
                    {
                        newItem = item.SubItems.Find(s => s.Key == edit.SubItems[x].Key);
                        if (newItem != null)
                            newItem.Value = edit.SubItems[x].Value;
                        else
                            item.SubItems.Add(new TyGlobalItem(edit.SubItems[x].Key, edit.SubItems[x].Value, item.Context, item.Indents + 4, ApplyGlobal_EqualSign(global, item, edit.SubItems[x])));
                    }
                    break;
                case TyModEdit.EditType.replace:
                    for (int x = 0; x < edit.SubItems.Count; x++)
                    {
                        newItem = item.SubItems.Find(s => s.Key == edit.SubItems[x].Key);
                        if (newItem != null)
                            newItem.Value = edit.SubItems[x].Value;
                    }
                    break;
            }
        }

        public static bool ApplyGlobal_EqualSign(TyGlobal global, TyGlobalItem root, TyModEditItem edit)
        {
            string[] mad = new string[] { "zwrite", "type", "effect", "id" };

            // Name is always defined without an equal sign
            if (edit.Key == "name")
                return false;

            // All definitions are set with the equal sign except name
            if (global.Name.EndsWith("sound"))
                return true;

            // Only select definitions have equal signs
            if (global.Name.EndsWith("mad") && mad.Contains(edit.Key))
                return true;

            // Harder to determine model entry
            // So we base off of the surrounding subitems
            if (global.Name.EndsWith("model"))
            {
                if (root.SubItems.Count > 0)
                    return root.SubItems[0].EqualSign;
            }

            // Default to true
            return true;
        }

        #endregion

    }

    public class TyModTranslate
    {
        public string Name { get; set; } = null;

        public Dictionary<string, string> Translations { get; set; } = null;

        public TyModTranslate(string name)
        {
            Name = name;

            Translations = new Dictionary<string, string>();
        }
    }

    public class TyModEdit
    {
        public enum EditType
        {
            inclusive,
            exclusive,
            replace
        }

        public EditType Type { get; } = EditType.inclusive;
        public string Source { get; } = null;
        public string Context { get; } = null;
        public Dictionary<string, string> Attributes { get; } = null;

        public string Key { get; set; } = null;
        public string Value { get; set; } = null;
        public List<TyModEditItem> SubItems { get; set; } = null;

        public TyModEdit(string source, EditType type, string context)
        {
            Source = source;
            Type = type;
            Context = context;

            Attributes = new Dictionary<string, string>();
            SubItems = new List<TyModEditItem>();
        }

        public override string ToString()
        {
            string result = Key + (Key == "name" ? " " : " = ") + (Value ?? "");
            foreach (TyModEditItem sub in SubItems)
                result += "\r\n\t" + sub.ToString();

            return result;
        }
    }

    public class TyModEditItem
    {
        public string Key { get; set; } = null;
        public string Value { get; set; } = null;
        public List<TyModEditItem> SubItems { get; set; } = null;

        public TyModEditItem(string key, string value)
        {
            Key = key;
            Value = value;
            SubItems = new List<TyModEditItem>();
        }

        public override string ToString()
        {
            string result = Key + (Key == "name" ? " " : " = ") + (Value ?? "");
            foreach (TyModEditItem sub in SubItems)
                result += "\r\n\t" + sub.ToString();

            return result;
        }
    }

}