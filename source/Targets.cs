using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Multiplayer.Transport;
using MegaCrit.Sts2.Core.Rewards;
namespace HostGold;
public static partial class Entry {
    static readonly HashSet<ulong> selectedPlayers=new();
    static readonly List<CheckBox> playerChecks=new();
    static GridContainer playerGrid;
    static Label targetSummary;
    static object selectionRun;
    static string roster="";
    static DateTime nextRoster=DateTime.MinValue;
    static string PlayerName(Player p){try{return PlatformUtil.GetPlayerName(RunManager.Instance.NetService.Platform,p.NetId)+" · "+p.Character.Title.GetFormattedText();}catch{return p.Character.Id.Entry+" · "+p.NetId;}}
    static List<Player> Targets()=>RunManager.Instance?.DebugOnlyGetState()?.Players.Where(p=>selectedPlayers.Contains(p.NetId)&&!p.Creature.IsDead).ToList()??new List<Player>();
    static void BuildTargets(VBoxContainer parent){
        parent=Section(parent);
        parent.AddChild(new Label {Text=L("k072")});
        var row=new HBoxContainer();row.AddThemeConstantOverride("separation",8);
        foreach(var mode in new[]{0,1,2}){int pick=mode;var button=new Button {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,Text=mode==0?L("k073"):mode==1?L("k074"):L("k075")};button.Pressed+=()=>{if(busy)return;selectedPlayers.Clear();var run=RunManager.Instance;var state=run?.DebugOnlyGetState();if(state!=null)foreach(var p in state.Players)if(!p.Creature.IsDead&&(pick==1||(pick==0&&p.NetId==run.NetService.NetId)))selectedPlayers.Add(p.NetId);UpdateTargetChecks();RefreshBrowsers();};row.AddChild(button);}parent.AddChild(row);
        var scroll=new ScrollContainer {CustomMinimumSize=new Vector2(400,64)};parent.AddChild(scroll);
        playerGrid=new GridContainer {Columns=2,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};playerGrid.AddThemeConstantOverride("h_separation",8);playerGrid.AddThemeConstantOverride("v_separation",6);scroll.AddChild(playerGrid);
        targetSummary=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(400,28)};MutedLabel(targetSummary);parent.AddChild(targetSummary);
    }
    static void RefreshTargets(bool force=false){
        foreach(var c in playerChecks)c.Disabled=busy||c.GetMeta("dead").AsBool();
        if(!force&&DateTime.UtcNow<nextRoster)return;nextRoster=DateTime.UtcNow.AddSeconds(1);
        var run=RunManager.Instance;var state=run?.DebugOnlyGetState();
        if(!ReferenceEquals(selectionRun,state)){selectionRun=state;selectedPlayers.Clear();if(state!=null)selectedPlayers.Add(run.NetService.NetId);roster="";}
        var players=state?.Players.ToList()??new List<Player>();
        string key=string.Join("|",players.Select(p=>p.NetId+":"+p.Creature.IsDead+":"+PlayerName(p)))+uiLanguage;
        selectedPlayers.RemoveWhere(id=>!players.Any(p=>p.NetId==id&&!p.Creature.IsDead));
        if(key!=roster||force){roster=key;foreach(Node n in playerGrid.GetChildren()){playerGrid.RemoveChild(n);n.QueueFree();}playerChecks.Clear();
            foreach(var p in players){var id=p.NetId;var box=new CheckBox {Text=PlayerName(p)+(id==run.NetService.NetId?L("k076"):""),Disabled=busy||p.Creature.IsDead,ClipText=true,CustomMinimumSize=new Vector2(195,38),TooltipText=PlayerName(p)};
                box.SetMeta("player_id",id);box.SetMeta("dead",p.Creature.IsDead);box.SetPressedNoSignal(selectedPlayers.Contains(id));box.Toggled+=v=>{if(busy)return;if(v)selectedPlayers.Add(id);else selectedPlayers.Remove(id);UpdateTargetChecks();RefreshBrowsers();};playerGrid.AddChild(box);playerChecks.Add(box);}}
        UpdateTargetChecks();
    }
    static void UpdateTargetChecks(){foreach(var c in playerChecks){c.SetPressedNoSignal(selectedPlayers.Contains(c.GetMeta("player_id").AsUInt64()));c.Disabled=busy||c.GetMeta("dead").AsBool();}
        int count=Targets().Count;targetSummary.Text=count==0?L("k077"):L("k078" ,count);}
    static bool ValidateTargets(List<Player> targets,out string reason){
        if(Check(out reason)==null)return false;
        var state=RunManager.Instance.DebugOnlyGetState();
        if(targets.Count==0||targets.Any(p=>!state.Players.Contains(p)||p.Creature.IsDead)){reason=L("k079");return false;}
        if(RunManager.Instance.NetService is NetHostGameService host && (host.ConnectedPeers.Any(p=>!p.readyForBroadcasting)||targets.Any(t=>t.NetId!=host.NetId&&!host.ConnectedPeers.Any(p=>p.peerId==t.NetId&&p.readyForBroadcasting)))){reason=L("k080");return false;}return true;
    }
    static void SyncForTarget(Player player,RewardObtainedMessage message){
        var run=RunManager.Instance;
        if(player.NetId==run.NetService.NetId){
            if(message.rewardType==RewardType.Gold)run.RewardSynchronizer.SyncLocalObtainedGold(message.goldAmount.Value);
            else if(message.rewardType==RewardType.Card)run.RewardSynchronizer.SyncLocalObtainedCard(message.cardModel);
            else if(message.rewardType==RewardType.Relic)run.RewardSynchronizer.SyncLocalObtainedRelic(message.relicModel);
            return;
        }
        if(run.NetService.Type!=NetGameType.Host||!compatible)throw new InvalidOperationException("Host-only reward routing unavailable.");
        message.location=run.RunLocationTargetedBuffer.CurrentLocation;
        var broadcast=run.NetService.GetType().GetMethod("BroadcastMessage",BindingFlags.NonPublic|BindingFlags.Instance);
        if(broadcast==null)throw new NotSupportedException("Targeted reward routing is not supported by this game build.");
        // Use the same sender metadata the host uses when relaying a client's reward.
        // Include the target client; only the host is excluded from network delivery.
        broadcast.MakeGenericMethod(typeof(RewardObtainedMessage)).Invoke(run.NetService,new object[]{message,run.NetService.NetId,message.Mode.ToChannelId(),player.NetId});
    }
    static bool MatchesCardFilter(CardModel card,string filter)=>filter=="*"||card.Pool.GetType().Name==filter;
    static readonly string[] cardFilters={"*","IroncladCardPool","SilentCardPool","RegentCardPool","NecrobinderCardPool","DefectCardPool","ColorlessCardPool","CurseCardPool","StatusCardPool","EventCardPool","TokenCardPool"};
    static string CharacterLabel(string pool,string fallback){try{var character=ModelDb.AllCharacters.FirstOrDefault(c=>c.CardPool.GetType().Name==pool);return character?.Title.GetFormattedText()??L(fallback);}catch{return L(fallback);}}
    static string[] CardFilterLabels()=>new[]{L("k081"),CharacterLabel("IroncladCardPool","k082"),CharacterLabel("SilentCardPool","k083"),CharacterLabel("RegentCardPool","k084"),CharacterLabel("NecrobinderCardPool","k085"),CharacterLabel("DefectCardPool","k086"),L("k087"),L("k088"),L("k089"),L("k090"),L("k091")};
    static void RefreshFilterLabels(){foreach(var b in browsers)if(b.Filter!=null){var labels=CardFilterLabels();for(int i=0;i<labels.Length;i++)b.Filter.SetItemText(i,labels[i]);}}
}

