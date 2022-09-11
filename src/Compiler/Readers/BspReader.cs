﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceSDK.Readers
{
    public class BspReader
    {
        private readonly FileStream bsp;
        private readonly bool isL4D2;
        private readonly KeyValuePair<int, int>[] offsets; // offset/length
        private readonly BinaryReader reader;
        
        private FileInfo _file { get; set; }

        private Dictionary<string, List<string>> _keys { get; }
        public BspReader(string file)
        {
            _file = new FileInfo(file);
            
            _keys = new Dictionary<string, List<string>>();
            _keys.Add("vmfSoundKeys", new List<string>());
            _keys.Add("vmfModelKeys", new List<string>());
            _keys.Add("vmfMaterialKeys", new List<string>());
            _keys.Add("vmtTextureKeyWords", new List<string>());
            _keys.Add("vmtMaterialKeyWords", new List<string>());

            offsets = new KeyValuePair<int, int>[64];
            using (bsp = new FileStream(_file.FullName, FileMode.Open))
            using (reader = new BinaryReader(bsp))
            {
                bsp.Seek(4, SeekOrigin.Begin); //skip header
                var bspVer = reader.ReadInt32();

                //hack for detecting l4d2 maps
                if (reader.ReadInt32() == 0 && bspVer == 21)
                {
                    isL4D2 = true;
                }

                // reset reader position
                bsp.Seek(-4, SeekOrigin.Current);

                //gathers an array of offsets (where things are located in the bsp)
                for (var i = 0; i < offsets.GetLength(0); i++)
                {
                    // l4d2 has different lump order
                    if (isL4D2)
                    {
                        bsp.Seek(4, SeekOrigin.Current); //skip version
                        offsets[i] = new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32());
                        bsp.Seek(4, SeekOrigin.Current); //skip id
                    }
                    else
                    {
                        offsets[i] = new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32());
                        bsp.Seek(8, SeekOrigin.Current); //skip id and version
                    }
                }

                buildEntityList();

                buildEntModelList();
                buildModelList();

                buildParticleList();

                buildEntTextureList();
                buildTextureList();

                buildEntSoundList();
            }
        }

        private string gameFolder;
        
        public List<Dictionary<string, string>> entityList { get; private set; }

        public List<List<Tuple<string, string>>> entityListArrayForm { get; private set; }

        public List<int>[] modelSkinList { get; private set; }

        public List<string> ModelList { get; private set; }

        public List<string> EntModelList { get; private set; }

        public List<string> ParticleList { get; private set; }

        public List<string> TextureList { get; private set; }
        public List<string> EntTextureList { get; private set; }

        public List<string> EntSoundList { get; private set; }

        // key/values as internalPath/externalPath
        private KeyValuePair<string, string> particleManifest { get; set; }
        private KeyValuePair<string, string> soundscript { get; set; }
        private KeyValuePair<string, string> soundscape { get; set; }
        private KeyValuePair<string, string> detail { get; set; }
        private KeyValuePair<string, string> nav { get; set; }
        private KeyValuePair<string, string> res { get; set; }
        private KeyValuePair<string, string> kv { get; set; }
        private KeyValuePair<string, string> txt { get; set; }
        private KeyValuePair<string, string> jpg { get; set; }
        private KeyValuePair<string, string> radartxt { get; set; }
        private List<KeyValuePair<string, string>> radardds { get; set; }
        private List<KeyValuePair<string, string>> languages { get; set; }
        private List<KeyValuePair<string, string>> VehicleScriptList { get; set; }
        private List<KeyValuePair<string, string>> EffectScriptList { get; set; }
        private List<string> vscriptList { get; set; }
        private List<KeyValuePair<string, string>> PanoramaMapIcons { get; set; }

      

        public void buildEntityList()
        {
            entityList = new List<Dictionary<string, string>>();
            entityListArrayForm = new List<List<Tuple<string, string>>>();

            bsp.Seek(offsets[0].Key, SeekOrigin.Begin);
            var ent = reader.ReadBytes(offsets[0].Value);
            var ents = new List<byte>();

            const int LCURLY = 123;
            const int RCURLY = 125;
            const int NEWLINE = 10;

            for (var i = 0; i < ent.Length; i++)
            {
                if (ent[i] == LCURLY && i + 1 < ent.Length)
                {
                    // if curly isnt followed by newline assume its part of filename
                    if (ent[i + 1] != NEWLINE)
                    {
                        ents.Add(ent[i]);
                    }
                }
                if (ent[i] != LCURLY && ent[i] != RCURLY)
                {
                    ents.Add(ent[i]);
                }
                else if (ent[i] == RCURLY)
                {
                    // if curly isnt followed by newline assume its part of filename
                    if (i + 1 < ent.Length && ent[i + 1] != NEWLINE)
                    {
                        ents.Add(ent[i]);
                        continue;
                    }


                    var rawent = Encoding.ASCII.GetString(ents.ToArray());
                    var entity = new Dictionary<string, string>();
                    var entityArrayFormat = new List<Tuple<string, string>>();
                    // split on \n, ignore \n inside of quotes
                    foreach (var s in Regex.Split(rawent, "(?=(?:(?:[^\"]*\"){2})*[^\"]*$)\\n"))
                    {
                        if (s.Count() != 0)
                        {
                            var c = s.Split('"');
                            if (!entity.ContainsKey(c[1]))
                            {
                                entity.Add(c[1], c[3]);
                            }
                            entityArrayFormat.Add(Tuple.Create(c[1], c[3]));
                        }
                    }
                    entityList.Add(entity);
                    entityListArrayForm.Add(entityArrayFormat);
                    ents = new List<byte>();
                }
            }
        }

        public void buildTextureList()
        {
            // builds the list of textures applied to brushes

            var mapname = bsp.Name.Split('\\').Last().Split('.')[0];

            TextureList = new List<string>();
            bsp.Seek(offsets[43].Key, SeekOrigin.Begin);
            TextureList = new List<string>(Encoding.ASCII.GetString(reader.ReadBytes(offsets[43].Value)).Split('\0'));
            for (var i = 0; i < TextureList.Count; i++)
            {
                if (TextureList[i].StartsWith("/")) // materials in root level material directory start with /
                {
                    TextureList[i] = "materials" + TextureList[i] + ".vmt";
                }
                else
                {
                    TextureList[i] = "materials/" + TextureList[i] + ".vmt";
                }
            }

            // find skybox materials
            var worldspawn = entityList.First(item => item["classname"] == "worldspawn");
            if (worldspawn.ContainsKey("skyname"))
            {
                foreach (var s in new[]
                {
                    "bk", "dn", "ft", "lf", "rt", "up"
                })
                {
                    TextureList.Add("materials/skybox/" + worldspawn["skyname"] + s + ".vmt");
                    TextureList.Add("materials/skybox/" + worldspawn["skyname"] + "_hdr" + s + ".vmt");
                }
            }

            // find detail materials
            if (worldspawn.ContainsKey("detailmaterial"))
            {
                TextureList.Add("materials/" + worldspawn["detailmaterial"] + ".vmt");
            }

            // find menu photos
            TextureList.Add("materials/vgui/maps/menu_photos_" + mapname + ".vmt");
        }

        public void buildEntTextureList()
        {
            // builds the list of textures referenced in entities

            EntTextureList = new List<string>();
            foreach (var ent in entityList)
            {
                var materials = new List<string>();
                foreach (var prop in ent)
                {
                    //Console.WriteLine(prop.Key + ": " + prop.Value);
                    if (_keys["vmfMaterialKeys"].Contains(prop.Key.ToLower()))
                    {
                        materials.Add(prop.Value);
                        if (prop.Key.ToLower().StartsWith("team_icon"))
                        {
                            materials.Add(prop.Value + "_locked");
                        }
                    }
                }


                // special condition for sprites
                if (ent["classname"].Contains("sprite") && ent.ContainsKey("model"))
                {
                    materials.Add(ent["model"]);
                }

                // special condition for item_teamflag
                if (ent["classname"].Contains("item_teamflag"))
                {
                    if (ent.ContainsKey("flag_trail"))
                    {
                        materials.Add("effects/" + ent["flag_trail"]);
                        materials.Add("effects/" + ent["flag_trail"] + "_red");
                        materials.Add("effects/" + ent["flag_trail"] + "_blu");
                    }
                    if (ent.ContainsKey("flag_icon"))
                    {
                        materials.Add("vgui/" + ent["flag_icon"]);
                        materials.Add("vgui/" + ent["flag_icon"] + "_red");
                        materials.Add("vgui/" + ent["flag_icon"] + "_blu");
                    }
                }

                // special condition for env_funnel. Hardcoded to use sprites/flare6.vmt
                if (ent["classname"].Contains("env_funnel"))
                {
                    materials.Add("sprites/flare6.vmt");
                }

                // special condition for env_embers. Hardcoded to use particle/fire.vmt
                if (ent["classname"].Contains("env_embers"))
                {
                    materials.Add("particle/fire.vmt");
                }

                // special condition for vgui_slideshow_display. directory paramater references all textures in a folder (does not include subfolders)
                if (ent["classname"].Contains("vgui_slideshow_display"))
                {
                    if (ent.ContainsKey("directory"))
                    {
                        var directory = $"{gameFolder}/materials/vgui/{ent["directory"]}";
                        if (Directory.Exists(directory))
                        {
                            foreach (var file in Directory.GetFiles(directory))
                            {
                                if (file.EndsWith(".vmt"))
                                {
                                    materials.Add($"/vgui/{ent["directory"]}/{Path.GetFileName(file)}");
                                }
                            }
                        }


                    }
                }

                // format and add materials
                foreach (var material in materials)
                {
                    var materialpath = material;
                    if (!material.EndsWith(".vmt") && !materialpath.EndsWith(".spr"))
                    {
                        materialpath += ".vmt";
                    }

                    EntTextureList.Add("materials/" + materialpath);
                }
            }

            // get all overlay mats
            var uniqueMats = new HashSet<string>();
            foreach (var ent in entityListArrayForm)
            {
                foreach (var kv in ent)
                {
                    var match = Regex.Match(kv.Item2, @"r_screenoverlay ([^,]+),");
                    if (match.Success)
                    {
                        uniqueMats.Add(match.Groups[1].Value.Replace(".vmt", ""));
                    }
                }
            }

            foreach (var mat in uniqueMats)
            {
                var path = string.Format("materials/{0}.vmt", mat);
                EntTextureList.Add(path);
            }
        }

        public void buildModelList()
        {
            // builds the list of models that are from prop_static

            ModelList = new List<string>();
            // getting information on the gamelump
            var propStaticId = 0;
            bsp.Seek(offsets[35].Key, SeekOrigin.Begin);
            var GameLumpOffsets = new KeyValuePair<int, int>[reader.ReadInt32()]; // offset/length
            for (var i = 0; i < GameLumpOffsets.Length; i++)
            {
                if (reader.ReadInt32() == 1936749168)
                {
                    propStaticId = i;
                }
                bsp.Seek(4, SeekOrigin.Current); //skip flags and version
                GameLumpOffsets[i] = new KeyValuePair<int, int>(reader.ReadInt32(), reader.ReadInt32());
            }

            // reading model names from game lump
            bsp.Seek(GameLumpOffsets[propStaticId].Key, SeekOrigin.Begin);
            var modelCount = reader.ReadInt32();
            for (var i = 0; i < modelCount; i++)
            {
                var model = Encoding.ASCII.GetString(reader.ReadBytes(128)).Trim('\0');
                if (model.Length != 0)
                {
                    ModelList.Add(model);
                }
            }

            // from now on we have models, now we want to know what skins they use

            // skipping leaf lump
            var leafCount = reader.ReadInt32();
            bsp.Seek(leafCount * 2, SeekOrigin.Current);

            // reading staticprop lump

            var propCount = reader.ReadInt32();

            //dont bother if there's no props, avoid a dividebyzero exception.
            if (propCount <= 0)
            {
                return;
            }

            var propOffset = bsp.Position;
            var byteLength = GameLumpOffsets[propStaticId].Key + GameLumpOffsets[propStaticId].Value - (int)propOffset;
            var propLength = byteLength / propCount;

            modelSkinList = new List<int>[modelCount]; // stores the ids of used skins

            for (var i = 0; i < modelCount; i++)
            {
                modelSkinList[i] = new List<int>();
            }

            for (var i = 0; i < propCount; i++)
            {
                bsp.Seek(i * propLength + propOffset + 24, SeekOrigin.Begin); // 24 skips origin and angles
                int modelId = reader.ReadUInt16();
                bsp.Seek(6, SeekOrigin.Current);
                var skin = reader.ReadInt32();

                if (modelSkinList[modelId].IndexOf(skin) == -1)
                {
                    modelSkinList[modelId].Add(skin);
                }
            }

        }

        public void buildEntModelList()
        {
            // builds the list of models referenced in entities

            EntModelList = new List<string>();
            foreach (var ent in entityList)
            {
                foreach (var prop in ent)
                {
                    if (ent["classname"].StartsWith("func"))
                    {
                        if (prop.Key == "gibmodel")
                        {
                            EntModelList.Add(prop.Value);
                        }
                    }
                    else if (!ent["classname"].StartsWith("trigger") &&
                             !ent["classname"].Contains("sprite"))
                    {
                        if (_keys["vmfModelKeys"].Contains(prop.Key))
                        {
                            EntModelList.Add(prop.Value);
                        }
                        // item_sodacan is hardcoded to models/can.mdl
                        // env_beverage spawns item_sodacans
                        else if (prop.Value == "item_sodacan" || prop.Value == "env_beverage")
                        {
                            EntModelList.Add("models/can.mdl");
                        }
                        // tf_projectile_throwable is hardcoded to  models/props_gameplay/small_loaf.mdl
                        else if (prop.Value == "tf_projectile_throwable")
                        {
                            EntModelList.Add("models/props_gameplay/small_loaf.mdl");
                        }
                    }
                }
            }
        }

        public void buildEntSoundList()
        {
            // builds the list of sounds referenced in entities
            char[] special_caracters =
            {
                '*', '#', '@', '>', '<', '^', '(', ')', '}', '$', '!', '?', ' '
            };
            EntSoundList = new List<string>();
            foreach (var ent in entityList)
            {
                foreach (var prop in ent)
                {
                    if (_keys["vmfSoundKeys"].Contains(prop.Key.ToLower()))
                    {
                        EntSoundList.Add("sound/" + prop.Value.Trim(special_caracters));
                    }
                    //Pack I/O triggered sounds
                    else if (prop.Value.Contains("PlayVO"))
                    {
                        //Parameter value following PlayVO is always either a sound path or an empty string
                        var io = prop.Value.Split(',').ToList();
                        if (!string.IsNullOrWhiteSpace(io[io.IndexOf("PlayVO") + 1]))
                        {
                            EntSoundList.Add("sound/" + io[io.IndexOf("PlayVO") + 1].Trim(special_caracters));
                        }
                    }
                    else if (prop.Value.Contains("playgamesound"))
                    {
                        var io = prop.Value.Split(',').ToList();
                        if (!string.IsNullOrWhiteSpace(io[io.IndexOf("playgamesound") + 1]))
                        {
                            EntSoundList.Add("sound/" + io[io.IndexOf("playgamesound") + 1].Trim(special_caracters));
                        }
                    }
                    else if (prop.Value.Contains("play"))
                    {
                        var io = prop.Value.Split(',').ToList();

                        var playCommand = io.Where(i => i.StartsWith("play "));

                        foreach (var command in playCommand)
                        {
                            EntSoundList.Add("sound/" + command.Split(' ')[1].Trim(special_caracters));
                        }
                    }

                }
            }


        }

        public void buildParticleList()
        {
            ParticleList = new List<string>();
            foreach (var ent in entityList)
            {
                foreach (var particle in ent)
                {
                    if (particle.Key.ToLower() == "effect_name")
                    {
                        ParticleList.Add(particle.Value);
                    }
                }
            }
        }
    }
}