using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;

// Registers source text with Godot's native translation system. Controls retain their
// original Text values, so gameplay and tests never depend on the displayed language.
public partial class Localization : Node
{
    private static readonly Dictionary<string,string[]> Exact=new(StringComparer.Ordinal);
    private sealed record Pattern(Regex Regex,string Replacement,string Language,string Prefix);
    private static readonly List<Pattern> Patterns=new();
    private static readonly List<(Regex Source,string English,string Thai)> Fragments=new();
    private static readonly HashSet<string> Seen=new(StringComparer.Ordinal);
    private static Translation _english=null!,_thai=null!;
    private static bool _loaded;
    private double _time;
    private readonly HashSet<Node> _targets=new();
    public override void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;Initialize();GamePreferences.Load();LocalizationCache.LoadCached("http://"+GameAccount.ServerHost+":7350");
        GetTree().NodeAdded+=Track;GetTree().NodeRemoved+=Forget;
        void Collect(Node node){Track(node);foreach(var child in node.GetChildren())Collect(child);}
        Collect(GetTree().Root);Scan(GetTree().Root);
    }
    private void Track(Node node){if(node is Label or Button or LineEdit)_targets.Add(node);}
    private void Forget(Node node)=>_targets.Remove(node);
    public override void _ExitTree(){GetTree().NodeAdded-=Track;GetTree().NodeRemoved-=Forget;}
    public static TranslationDocument BundledDocument()=>JsonSerializer.Deserialize(Godot.FileAccess.GetFileAsString("res://data/ui_translations.json"),GameJsonContext.Default.TranslationDocument)??throw new Exception("Missing bundled translations");
    public static void Initialize(){if(!_loaded)ApplyDocument(BundledDocument());}
    public static void ApplyDocument(TranslationDocument data)
    {
        data.Validate();
        if(_loaded){TranslationServer.RemoveTranslation(_english);TranslationServer.RemoveTranslation(_thai);}
        _loaded=true;Exact.Clear();Patterns.Clear();Fragments.Clear();Seen.Clear();
        _english=new Translation {Locale="en"};_thai=new Translation {Locale="th"};
        // Keep new client text translated while an older server catalog is cached.
        foreach(var pair in data.Messages.Concat(BundledDocument().Messages.Where(p=>!data.Messages.Any(remote=>remote[0]==p[0])))){
            string sourceEn=pair[0],sourceTh=pair[1],en=pair.Length==4?pair[2]:pair[0],th=pair.Length==4?pair[3]:pair[1];
            if(Regex.IsMatch(sourceEn,@"\{\d+\}")){
                AddPattern(sourceEn,en,"en");AddPattern(sourceEn,th,"th");AddPattern(sourceTh,en,"en");AddPattern(sourceTh,th,"th");
            }
            else{Exact[sourceEn]=new[]{en,th};Exact[sourceTh]=new[]{en,th};Register(sourceEn,en,th);Register(sourceTh,en,th);}
        }
        foreach(var pair in data.Fragments){
            string en=pair.Length==4?pair[2]:pair[0],th=pair.Length==4?pair[3]:pair[1];
            Fragments.Add((new Regex(@"(?<![\p{L}\p{N}_])"+Regex.Escape(pair[0])+@"(?![\p{L}\p{N}_])|"+Regex.Escape(pair[1])),en,th));
        }
        Patterns.Sort((a,b)=>b.Regex.ToString().Length.CompareTo(a.Regex.ToString().Length));
        TranslationServer.AddTranslation(_english);TranslationServer.AddTranslation(_thai);
        if(Engine.GetMainLoop() is SceneTree tree)Refresh(tree.Root);
    }
    private static void AddPattern(string source,string target,string language)
    {
        string expression=Regex.Escape(source);
        foreach(var match in Regex.Matches(source,@"\{(\d+)\}").Cast<Match>().DistinctBy(m=>m.Value))expression=expression.Replace(Regex.Escape(match.Value),$"(?<v{match.Groups[1].Value}>.+?)");
        Patterns.Add(new Pattern(new Regex("^"+expression+"$",RegexOptions.Singleline),target,language,source.Split('{')[0]));
    }
    private static void Register(string source,string english,string thai){_english.AddMessage(source,english);_thai.AddMessage(source,thai);Seen.Add(source);}
    public static string Translate(string source,string language)
    {
        Initialize();bool thai=language=="th";
        if(Exact.TryGetValue(source,out var pair))return pair[thai?1:0];
        if(source.Contains('\n'))return string.Join("\n",source.Split('\n').Select(s=>Translate(s,language)));
        if(source.Contains(" • "))return string.Join(" • ",source.Split(" • ").Select(s=>Translate(s,language)));
        var numbered=Regex.Match(source,@"^(\d+(?:–\d+)?\.\s+)(.+)$");
        if(numbered.Success)return numbered.Groups[1].Value+Translate(numbered.Groups[2].Value,language);
        foreach(var pattern in Patterns){
            if(pattern.Language!=language||!source.StartsWith(pattern.Prefix,StringComparison.Ordinal))continue;
            var match=pattern.Regex.Match(source);if(!match.Success)continue;
            string result=Regex.Replace(pattern.Replacement,@"\{(\d+)\}",m=>{string captured=match.Groups["v"+m.Groups[1].Value].Value;return captured==source?captured:Translate(captured,language);});
            return ReplaceFragments(result,thai);
        }
        if(source.Contains('\n'))return string.Join("\n",source.Split('\n').Select(s=>Translate(s,language)));
        if(source.Contains(" • "))return string.Join(" • ",source.Split(" • ").Select(s=>Translate(s,language)));
        string resultText=source;
        if(!thai)foreach(var item in Exact.Where(p=>p.Key.Length>2&&Regex.IsMatch(p.Key,"[ก-๙]")).OrderByDescending(p=>p.Key.Length))resultText=resultText.Replace(item.Key,item.Value[0],StringComparison.Ordinal);
        return ReplaceFragments(resultText,thai);
    }
    private static string ReplaceFragments(string text,bool thai)
    {
        foreach(var f in Fragments)text=f.Source.Replace(text,_=>thai?f.Thai:f.English);
        return text;
    }
    public static string Text(string source)
    {
        return Translate(source,GamePreferences.Language);
    }
    private static bool Ensure(string source)
    {
        if(string.IsNullOrWhiteSpace(source)||Seen.Contains(source))return false;
        string en=Translate(source,"en"),th=Translate(source,"th");
        Register(source,en,th);return en!=source||th!=source;
    }
    public static void Refresh(Node root){Initialize();Scan(root);root.PropagateNotification((int)Node.NotificationTranslationChanged);}
    private static bool RegisterNode(Node node)
    {
        if(!node.CanAutoTranslate())return false;
        bool changed=false;
        if(node is Label label)changed|=Ensure(label.Text);
        if(node is Button button)changed|=Ensure(button.Text);
        if(node is LineEdit edit)changed|=Ensure(edit.PlaceholderText);
        if(node is OptionButton options)for(int i=0;i<options.ItemCount;i++)changed|=Ensure(options.GetItemText(i));
        return changed;
    }
    private static void Scan(Node root)
    {
        bool changed=RegisterNode(root);
        void Visit(Node node){changed|=RegisterNode(node);foreach(var child in node.GetChildren())Visit(child);}
        foreach(var child in root.GetChildren())Visit(child);
        if(changed)root.PropagateNotification((int)Node.NotificationTranslationChanged);
    }
    public override void _Process(double delta)
    {
        _time+=delta;if(_time<.15)return;_time=0;bool changed=false;
        foreach(var node in _targets)changed|=RegisterNode(node);
        if(changed)GetTree().Root.PropagateNotification((int)Node.NotificationTranslationChanged);
    }
}
