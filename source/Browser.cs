using System;
using MegaCrit.Sts2.Core.Multiplayer.Messages.Game.Sync;
using MegaCrit.Sts2.Core.Rewards;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace HostGold;
public static partial class Entry {
    sealed class Browser {
        public bool Cards;
        public LineEdit Search;
        public OptionButton Filter;
        public ItemList List;
        public Label Detail;
        public Label CountLabel;
        public SpinBox Count;
        public Button Add;
        public List<AbstractModel> Items=new List<AbstractModel>();
        public AbstractModel Selected;
    }
    static readonly List<Browser> browsers=new List<Browser>();
    static string ItemTitle(AbstractModel model) {
        try {return model is CardModel c?c.Title:((RelicModel)model).Title.GetFormattedText();}
        catch {return model.Id.Entry;}
    }
    static bool IsVanilla(AbstractModel model)=>model.GetType().Assembly==typeof(ModelDb).Assembly;
    static void BuildBrowser(TabContainer tabs,bool cards) {
        var b=new Browser {Cards=cards};browsers.Add(b);
        var page=new VBoxContainer {Name=cards?L("k000"):L("k001")};page.AddThemeConstantOverride("separation",8);tabs.AddChild(page);
        var searchRow=new HBoxContainer();searchRow.AddThemeConstantOverride("separation",8);page.AddChild(searchRow);
        if(cards){b.Filter=new OptionButton {CustomMinimumSize=new Vector2(145,40)};foreach(string label in CardFilterLabels())b.Filter.AddItem(label);b.Filter.ItemSelected+=_=>RefreshBrowser(b);searchRow.AddChild(b.Filter);}
        b.Search=new LineEdit {PlaceholderText=L("k002"),ClearButtonEnabled=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};searchRow.AddChild(b.Search);
        b.CountLabel=new Label {Text=L("k003")};MutedLabel(b.CountLabel);page.AddChild(b.CountLabel);
        b.List=new ItemList {CustomMinimumSize=new Vector2(400,215),SizeFlagsVertical=Control.SizeFlags.ExpandFill};page.AddChild(b.List);
        b.Detail=new Label {Text="",AutowrapMode=TextServer.AutowrapMode.WordSmart,CustomMinimumSize=new Vector2(400,45)};page.AddChild(b.Detail);
        if(cards){
            var row=new HBoxContainer();row.AddChild(new Label {Text=L("k005")});
            b.Count=new SpinBox {MinValue=1,MaxValue=10,Step=1,Value=1,UpdateOnTextChanged=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};row.AddChild(b.Count);page.AddChild(row);
        }else page.AddChild(new Label {Text=L("k006")});
        b.Add=new Button {ThemeTypeVariation="HostPrimary",Text=cards?L("k007"):L("k008"),Disabled=true};page.AddChild(b.Add);
        b.Search.TextChanged+=_=>RefreshBrowser(b);
        b.List.ItemSelected+=index=>{
            b.Selected=index>=0&&index<b.Items.Count?b.Items[(int)index]:null;
            b.Detail.Text=b.Selected==null?L("k009"):ItemTitle(b.Selected)+"\n"+b.Selected.Id.Entry;
        };
        b.Add.Pressed+=()=>{_ = AddSelected(b);};
        if(cards){
            removeButton=new Button {Text=L("k092"),TooltipText=L("k093")};
            removeButton.Pressed+=()=>{_ = RemoveLocalCard();};page.AddChild(removeButton);
        }
    }
    static void RefreshBrowser(Browser b) {
        b.Selected=null;b.Items.Clear();b.List.Clear();b.Detail.Text="";
        if(Check(out _) == null){b.CountLabel.Text=L("k010");return;}
        try {
            var query=b.Search.Text.Trim();
            IEnumerable<AbstractModel> models=b.Cards?ModelDb.AllCards.Cast<AbstractModel>():ModelDb.AllRelics.Cast<AbstractModel>();
            var me=Check(out _);
            b.Items=models.Where(IsVanilla).Where(m=>!b.Cards||MatchesCardFilter((CardModel)m,cardFilters[b.Filter.Selected])).Where(m=>query.Length==0||ItemTitle(m).Contains(query,StringComparison.OrdinalIgnoreCase)||m.Id.Entry.Contains(query,StringComparison.OrdinalIgnoreCase))
                .OrderBy(ItemTitle,StringComparer.CurrentCulture).ToList();
            foreach(var m in b.Items){
                bool owned=!b.Cards&&Targets().Count>0&&Targets().All(p=>p.Relics.Any(r=>r.Id==m.Id));
                b.List.AddItem(ItemTitle(m)+(owned?L("k011"):"")+"  ·  "+m.Id.Entry);
            }
            b.CountLabel.Text=b.Items.Count==0?L("k012"):L("k013")+b.Items.Count;
        }catch(Exception e){b.CountLabel.Text=L("k015");GD.PrintErr("[HostGold] Browser: "+e);}
    }
    static void RefreshBrowsers(){foreach(var b in browsers)RefreshBrowser(b);}
    static void UpdateBrowserButtons(bool disabled) {
        var me=Check(out _);
        if(removeButton!=null)removeButton.Disabled=disabled||Targets().Count!=1||me==null||Targets()[0]!=me;
        foreach(var b in browsers){
            bool owned=b.Selected is RelicModel && Targets().Count>0 && Targets().All(p=>p.Relics.Any(r=>r.Id==b.Selected.Id));
            b.Add.Disabled=disabled||b.Selected==null||owned;
            b.Add.Text=owned?L("k016"):b.Cards?L("k007"):L("k008");
            if(b.Filter!=null)b.Filter.Disabled=busy;
            b.Search.Editable=!busy;b.List.MouseFilter=busy?Control.MouseFilterEnum.Ignore:Control.MouseFilterEnum.Stop;
            if(b.Count!=null)b.Count.Editable=!busy;
        }
    }
    static Button removeButton;
    static async Task RemoveLocalCard() {
        if(busy||(DateTime.UtcNow-last).TotalSeconds<2)return;
        var me=Check(out string reason);var targets=Targets();
        if(me==null){resultLabel.Text=reason;return;}
        if(targets.Count!=1||targets[0]!=me){resultLabel.Text=L("k093");return;}
        if(!ValidateTargets(targets,out reason)){resultLabel.Text=reason;return;}
        busy=true;last=DateTime.UtcNow;panel.Hide();
        try {
            // The vanilla flow synchronizes selection, supports cancellation and requires confirmation.
            bool removed=await RunManager.Instance.RewardSynchronizer.DoLocalCardRemoval();
            resultLabel.Text=L(removed?"k094":"k095");
        }catch(Exception e){resultLabel.Text=L("k096");GD.PrintErr("[HostGold] Remove card: "+e);}
        finally{busy=false;last=DateTime.UtcNow;panel.Show();RefreshBrowsers();}
    }
    static async Task AddSelected(Browser browser) {
        if(busy||(DateTime.UtcNow-last).TotalSeconds<2)return;
        var targets=Targets();if(!ValidateTargets(targets,out string reason)){resultLabel.Text=reason;return;}
        var selected=browser.Selected;if(selected==null||!IsVanilla(selected))return;
        if(selected is RelicModel)targets=targets.Where(p=>!p.Relics.Any(r=>r.Id==selected.Id)).ToList();
        if(targets.Count==0){resultLabel.Text=L("k017");return;}
        int count=browser.Cards?(int)browser.Count.Value:1;if(count<1||count>10)return;
        var run=RunManager.Instance;var state=run.DebugOnlyGetState();
        busy=true;last=DateTime.UtcNow;int completed=0;int total=targets.Count*count;
        if(prefs.AutoClose)panel.Hide();
        try {
            foreach(var player in targets)for(int i=0;i<count;i++){
                if(!ReferenceEquals(state,run.DebugOnlyGetState())||!ValidateTargets(targets,out reason))throw new InvalidOperationException(reason);
                if(selected is CardModel template){
                    var card=state.CreateCard(template,player);
                    SyncForTarget(player,new RewardObtainedMessage {location=RunManager.Instance.RunLocationTargetedBuffer.CurrentLocation,wasSkipped=false,rewardType=RewardType.Card,cardModel=card});
                    await CardPileCmd.Add(card,PileType.Deck,CardPilePosition.Bottom,null,false);
                }else {
                    var relic=((RelicModel)selected).ToMutable();
                    SyncForTarget(player,new RewardObtainedMessage {location=RunManager.Instance.RunLocationTargetedBuffer.CurrentLocation,wasSkipped=false,rewardType=RewardType.Relic,relicModel=relic});
                    await RelicCmd.Obtain(relic,player,-1);
                }
                completed++;
            }
            resultLabel.Text=L("k018" ,completed,targets.Count);
        }catch(Exception e){resultLabel.Text=L("k019" ,completed,total);GD.PrintErr("[HostGold] Add item: "+e);panel.Show();}
        finally{busy=false;last=DateTime.UtcNow;}
    }
}

