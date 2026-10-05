using Godot;
using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

// Explicit offline tool: preserve gameplay fields and generate catalog copy from combat rules.
public partial class CharacterCopyExporter : Node
{
    public override void _Ready()
    {
        try {
            var paths=OS.GetCmdlineUserArgs();
            if(paths.Length==0)throw new Exception("Pass catalog JSON paths after --.");
            foreach(string path in paths){
                using var source=JsonDocument.Parse(File.ReadAllText(path));
                using var output=new MemoryStream();
                using(var writer=new Utf8JsonWriter(output,new JsonWriterOptions {Indented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping})){
                    writer.WriteStartObject();
                    foreach(var property in source.RootElement.EnumerateObject()){
                        if(property.Name!="characters"){property.WriteTo(writer);continue;}
                        writer.WriteStartArray("characters");
                        foreach(var character in property.Value.EnumerateArray()){
                            int kind=character.GetProperty("kind").GetInt32();
                            string passive=CharacterData.PassiveDescriptionFor(kind,1);
                            writer.WriteStartObject();
                            foreach(var field in character.EnumerateObject()){
                                string? copy=field.Name switch {
                                    "description"=>CharacterData.CombatDescription(kind,1),
                                    "ability_name"=>"ความสามารถ",
                                    "ability_description"=>CharacterData.AttackDescription(kind,1),
                                    "passive_name"=>passive.Length>0?"ความสามารถติดตัว":"",
                                    "passive_description"=>passive,
                                    _=>null
                                };
                                if(copy==null)field.WriteTo(writer);else writer.WriteString(field.Name,copy);
                            }
                            writer.WriteEndObject();
                        }
                        writer.WriteEndArray();
                    }
                    writer.WriteEndObject();
                }
                File.WriteAllText(path,Encoding.UTF8.GetString(output.ToArray())+"\n",new UTF8Encoding(false));
                GD.Print("Updated character descriptions: "+path);
            }
            GetTree().Quit();
        }catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
