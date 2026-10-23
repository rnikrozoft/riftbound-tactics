using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class DeckAnalysis : VBoxContainer
{
    public sealed record Category(string Name,Func<CharacterDefinition,bool> Matches);
    public static AttackRule[] Actions(CharacterDefinition c)=>c.Combat==null?Array.Empty<AttackRule>():c.Combat.Actions.Concat(c.Combat.First==null?Array.Empty<AttackRule>():new[]{c.Combat.First}).ToArray();
    public static readonly Category[] Categories={
        new("Melee",c=>Actions(c).Any(a=>a.Mode=="melee")),
        new("Ranged",c=>Actions(c).Any(a=>a.Mode=="ranged")),
        new("Support",c=>Actions(c).Any(a=>a.Mode is "heal" or "curse" or "revive" or "summon")),
        new("Protect allies",c=>c.Combat?.Bodyguard==true),
        new("Heal allies",c=>Actions(c).Any(a=>a.Heal>0||a.TeamHeal>0)),
        new("Area damage",c=>Actions(c).Any(a=>a.Splash!=""&&a.SplashPower>0)||!string.IsNullOrEmpty(c.Combat?.KillSplash)),
        new("Control",c=>Actions(c).Any(a=>a.Mode=="curse"||a.Status=="stun"||a.SplashStatus=="stun"||a.SwapBack)),
        new("Armor piercing",c=>Actions(c).Any(a=>a.Pierce>0)),
        new("Fire / Poison",c=>Actions(c).Any(a=>a.Status is "fire" or "poison"||a.SplashStatus is "fire" or "poison")||c.Combat?.KillStatus is "fire" or "poison")
    };
    private readonly DeckDefinition _deck;
    private readonly List<TextureButton> _buttons=new();
    private VBoxContainer _members=null!,_detail=null!;
    public DeckAnalysis():this(DeckDefinition.Starter()){}
    public DeckAnalysis(DeckDefinition deck){_deck=deck;SizeFlagsHorizontal=SizeFlags.ExpandFill;AddThemeConstantOverride("separation",8);}
    public int Copies(Category category)=>_deck.Cards.Where(e=>category.Matches(CharacterData.Get(e.Kind))).Sum(e=>e.Copies);
    public override void _Ready()
    {
        AddChild(MenuUi.Eyebrow("DECK ROLES & ABILITIES · 1–8★"));
        var grid=new GridContainer {Columns=3,SizeFlagsHorizontal=SizeFlags.ExpandFill};AddChild(grid);
        foreach(var category in Categories){
            int kinds=_deck.Cards.Count(e=>category.Matches(CharacterData.Get(e.Kind)));
            var button=DeckMenuUi.Button($"{category.Name}  {kinds}",()=>ShowCategory(category),94,32);
            button.SizeFlagsHorizontal=SizeFlags.ExpandFill;button.GetNode<Label>("Text").AddThemeFontSizeOverride("font_size",11);
            button.Disabled=kinds==0;grid.AddChild(button);_buttons.Add(button);
        }
        _members=new VBoxContainer();AddChild(_members);
        _detail=new VBoxContainer();AddChild(_detail);
        ShowCategory(Categories.Skip(3).FirstOrDefault(c=>Copies(c)>0)??Categories.FirstOrDefault(c=>Copies(c)>0)??Categories[0]);
    }
    public void ShowCategory(Category category)
    {
        for(int i=0;i<_buttons.Count;i++)GameUi.Selected(_buttons[i],Categories[i]==category);
        DeckMenuUi.Clear(_members);DeckMenuUi.Clear(_detail);
        var entries=_deck.Cards.Where(e=>category.Matches(CharacterData.Get(e.Kind))).ToArray();
        var count=DeckMenuUi.Text($"{category.Name} · {entries.Length} kinds / {Copies(category)} copies",12);count.AutowrapMode=TextServer.AutowrapMode.WordSmart;_members.AddChild(count);
        var portraits=new GridContainer {Columns=7};portraits.AddThemeConstantOverride("h_separation",5);portraits.AddThemeConstantOverride("v_separation",5);_members.AddChild(portraits);
        foreach(var entry in entries){
            int kind=entry.Kind;var button=DeckMenuUi.Button("",()=>ShowCharacter(kind,1),34,42);button.Name="AnalysisCharacter_"+kind;portraits.AddChild(button);
            var art=DeckMenuUi.Art(kind,30,38);art.GetNode<Control>("ClassEmblem").Hide();art.MouseFilter=MouseFilterEnum.Ignore;button.AddChild(art);art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }
        if(entries.Length>0)ShowCharacter(entries[0].Kind,1);
    }
    public void ShowCharacter(int kind,int stars)
    {
        DeckMenuUi.Clear(_detail);
        var entry=_deck.Cards.First(e=>e.Kind==kind);
        var name=DeckMenuUi.Text($"{CardCatalog.Name(kind)} · ×{entry.Copies}",15);name.AutowrapMode=TextServer.AutowrapMode.WordSmart;_detail.AddChild(name);
        var tiers=new HBoxContainer();_detail.AddChild(tiers);
        for(int i=1;i<=4;i++){int tier=i;var button=DeckMenuUi.Button(CharacterData.DisplayStars(tier)+"★",()=>ShowCharacter(kind,tier),46,28);GameUi.Selected(button,stars==tier);tiers.AddChild(button);}
        var description=DeckMenuUi.Text(CharacterData.CombatDescription(kind,stars),13);description.AutowrapMode=TextServer.AutowrapMode.WordSmart;_detail.AddChild(description);
    }
}
