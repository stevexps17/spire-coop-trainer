using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
namespace HostGold;
public static partial class Entry {
    static readonly HashSet<ulong> energyPlayers=new(), protectionPlayers=new();
    static object assistRun;
    static ConsoleCmdGameAction assistPending;
    static DateTime assistSent, assistNext;
    static Player assistPlayer;
    static object assistCombat;
    static bool assistWasEnergy;
    static int assistBefore;
    static int EnergyRefill(int current)=>current<3?(int)Math.Min(1000L,10L-current):0;
    static int BufferAmount(Player player)=>player.Creature.Powers.OfType<BufferPower>().FirstOrDefault()?.Amount??0;
    static bool assistStopped;
    static Label assistStatus;
    static readonly List<Button> assistEnableButtons=new();
    static readonly MethodInfo enqueueAssist=typeof(ActionQueueSynchronizer).GetMethod("EnqueueAction",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
    static readonly FieldInfo assistQueues=typeof(ActionQueueSynchronizer).GetField("_actionQueueSet",BindingFlags.Instance|BindingFlags.NonPublic);
    static void BuildCombatAssist(TabContainer tabs) {
        var page=new VBoxContainer {Name=L("k097")};page.AddThemeConstantOverride("separation",8);tabs.AddChild(page);
        foreach(bool energy in new[]{true,false}) {
            page.AddChild(new Label {Text=L(energy?"k098":"k099")});
            var row=new HBoxContainer();page.AddChild(row);
            foreach(bool enable in new[]{true,false}) {
                var button=new Button {Text=L(enable?"k100":"k101"),TooltipText=L(enable?"k100":"k101"),ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};row.AddChild(button);
                if(enable)assistEnableButtons.Add(button);
                button.Pressed+=()=>SetAssist(energy,enable);
            }
        }
        var stop=new Button {Text=L("k102")};stop.Pressed+=()=>{energyPlayers.Clear();protectionPlayers.Clear();};page.AddChild(stop);
        page.AddChild(new Label {Text=L("k103"),AutowrapMode=TextServer.AutowrapMode.WordSmart});
        assistStatus=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};page.AddChild(assistStatus);
    }
    static bool AssistAllowed() {
        var run=RunManager.Instance;var net=run?.NetService;
        return compatible&&run!=null&&run.IsInProgress&&!run.IsGameOver&&!run.IsCleaningUp&&net!=null&&!net.IsGameLoading&&
            (net.Type==NetGameType.Singleplayer||(net.Type==NetGameType.Host&&net.IsConnected&&((NetHostGameService)net).ConnectedPeers.All(p=>p.readyForBroadcasting)));
    }
    static void SetAssist(bool energy,bool enable) {
        var ids=energy?energyPlayers:protectionPlayers;
        if(enable&&(!AssistAllowed()||assistStopped||assistPending!=null))return;
        foreach(var p in Targets()) {if(enable)ids.Add(p.NetId);else ids.Remove(p.NetId);}
    }
    static void StopAssist(Exception error) {
        energyPlayers.Clear();protectionPlayers.Clear();assistStopped=true;
        resultLabel.Text=L("k105");GD.PrintErr("[HostGold] Combat assist stopped: "+error);panel.Show();
    }
    static void TickCombatAssist() {
        try {
            var run=RunManager.Instance;var state=run?.DebugOnlyGetState();
            if(!ReferenceEquals(assistRun,state)) {assistRun=state;energyPlayers.Clear();protectionPlayers.Clear();assistPending=null;assistStopped=false;}
            string Names(HashSet<ulong> ids)=>state==null?"—":string.Join(", ",state.Players.Where(p=>ids.Contains(p.NetId)).Select(PlayerName));
            if(assistStatus!=null)assistStatus.Text=assistStopped?L("k105"):L("k104",Names(energyPlayers),Names(protectionPlayers));
            foreach(var button in assistEnableButtons)button.Disabled=!AssistAllowed()||assistStopped||assistPending!=null||Targets().Count==0;
            if(assistPending!=null) {
                if(assistPending.CompletionTask.IsCompleted) {
                    var failed=assistPending.Exception;assistPending=null;assistNext=DateTime.UtcNow.AddMilliseconds(400);
                    if(failed==null&&assistPlayer!=null&&!assistPlayer.Creature.IsDead&&ReferenceEquals(assistCombat,CombatManager.Instance.DebugOnlyGetState())&&
                        (assistWasEnergy?assistPlayer.PlayerCombatState.Energy:BufferAmount(assistPlayer))<=assistBefore)
                        failed=new InvalidOperationException("Command completed without a confirmed increase; stopping instead of retrying.");
                    if(failed!=null)StopAssist(failed);
                }else if(!assistStopped&&(DateTime.UtcNow-assistSent).TotalSeconds>15)StopAssist(new TimeoutException("Action pending; no retries until a new run."));
                return;
            }
            if(assistStopped||!AssistAllowed()||busy||DateTime.UtcNow<assistNext)return;
            var combat=CombatManager.Instance;
            if(!combat.IsInProgress||combat.IsStarting||combat.IsEnding||combat.PlayerActionsDisabled||combat.IsEnemyTurnStarted||combat.EndingPlayerTurnPhaseOne||combat.EndingPlayerTurnPhaseTwo||run.ActionExecutor.IsRunning)return;
            if(assistQueues?.GetValue(run.ActionQueueSynchronizer) is not ActionQueueSet queues||!queues.IsEmpty)return;
            var cs=combat.DebugOnlyGetState();if(cs==null)return;
            foreach(var p in state.Players) {
                if(p.Creature.IsDead||p.PlayerCombatState==null||p.PlayerCombatState.Phase!=PlayerTurnPhase.Play)continue;
                if(run.NetService is NetHostGameService host&&p.NetId!=host.NetId&&!host.ConnectedPeers.Any(x=>x.peerId==p.NetId))continue;
                // Native Buffer only prevents losses using the game's normal HP-loss hooks.
                if(protectionPlayers.Contains(p.NetId)&&BufferAmount(p)<10) {
                    int index=cs.Creatures.ToList().IndexOf(p.Creature);
                    if(index>=0){SendAssist(p,"power BUFFER 100 "+index);return;}
                }
                if(energyPlayers.Contains(p.NetId)&&p.PlayerCombatState.Energy<3) {SendAssist(p,"energy "+EnergyRefill(p.PlayerCombatState.Energy));return;}
            }
        }catch(Exception e){if(!assistStopped)StopAssist(e);}
    }
    static void SendAssist(Player player,string command) {
        // Owner and message playerId must agree for every peer to reconstruct the same action.
        assistPlayer=player;assistCombat=CombatManager.Instance.DebugOnlyGetState();assistWasEnergy=command.StartsWith("energy ",StringComparison.Ordinal);
        assistBefore=assistWasEnergy?player.PlayerCombatState.Energy:BufferAmount(player);
        var action=new ConsoleCmdGameAction(player,command,true);assistPending=action;assistSent=DateTime.UtcNow;
        enqueueAssist.Invoke(RunManager.Instance.ActionQueueSynchronizer,new object[]{action,player.NetId});
    }
}


