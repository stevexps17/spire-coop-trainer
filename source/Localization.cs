using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;
namespace HostGold;
public static partial class Entry {
    static string uiLanguage="eng";
    static readonly string[] SupportedLanguages={"eng","zhs","zht","deu","esp","fra","ind","ita","jpn","kor","pol","ptb","rus","spa","tha","tur"};
    static readonly Dictionary<string,Dictionary<string,string>> catalogs=new();
    sealed class RenderedText {public string Key;public object[] Args;}
    static readonly Dictionary<string,RenderedText> translations=new();
    static DateTime nextLanguageCheck=DateTime.MinValue;
    static void InitializeLanguage(){try {uiLanguage=NormalizeLanguage(LocManager.Instance.Language);}catch {uiLanguage="eng";}}
    static string NormalizeLanguage(string language){
        var code=(language??"eng").ToLowerInvariant().Replace('_','-');
        foreach(var supported in SupportedLanguages)if(code==supported)return code;
        return code switch {
            "zh" or "zh-cn" or "zh-sg" or "zh-hans"=>"zhs",
            "zh-tw" or "zh-hk" or "zh-hant"=>"zht",
            "en" or "en-us" or "en-gb"=>"eng", "de"=>"deu",
            "es-419" or "es-mx" or "latam"=>"esp", "es" or "es-es"=>"spa",
            "fr"=>"fra", "id"=>"ind", "it"=>"ita", "ja"=>"jpn", "ko"=>"kor",
            "pl"=>"pol", "pt" or "pt-br"=>"ptb", "ru"=>"rus", "th"=>"tha", "tr"=>"tur", _=>"eng"
        };
    }
    static Dictionary<string,string> Catalog(string code){
        if(catalogs.TryGetValue(code,out var known))return known;
        using var stream=typeof(Entry).Assembly.GetManifestResourceStream("HostGold.i18n."+code+".json");
        if(stream==null)throw new InvalidDataException("Missing language resource: "+code);
        using var reader=new StreamReader(stream);
        var table=JsonSerializer.Deserialize<Dictionary<string,string>>(reader.ReadToEnd());catalogs[code]=table;return table;
    }
    static string ResolveText(string code,string key,object[] args){
        if(!Catalog(code).TryGetValue(key,out var value)&&!Catalog("eng").TryGetValue(key,out value))return key;
        return args.Length==0?value:string.Format(CultureInfo.InvariantCulture,value,args);
    }
    static string L(string key,params object[] args){
        string value=ResolveText(uiLanguage,key,args);
        if(translations.Count>4096)translations.Clear();
        translations[value]=new RenderedText {Key=key,Args=(object[])args.Clone()};return value;
    }
    static string TranslateExisting(string value){
        if(value!=null&&translations.TryGetValue(value,out var rendered))return L(rendered.Key,rendered.Args);
        return value;
    }
    static void TranslateControls(Node root){
        if(root is Label label)label.Text=TranslateExisting(label.Text);
        if(root is Button button){button.Text=TranslateExisting(button.Text);button.ClipText=true;button.TooltipText=button.Text;}
        if(root is LineEdit edit)edit.PlaceholderText=TranslateExisting(edit.PlaceholderText);
        if(root is Control control)control.TooltipText=TranslateExisting(control.TooltipText);
        if(root is TabContainer tabs)for(int i=0;i<tabs.GetTabCount();i++)tabs.SetTabTitle(i,TranslateExisting(tabs.GetTabTitle(i)));
        foreach(Node child in root.GetChildren())TranslateControls(child);
    }
    static void PollLanguage(){
        if(DateTime.UtcNow<nextLanguageCheck)return;
        nextLanguageCheck=DateTime.UtcNow.AddMilliseconds(500);
        string current=NormalizeLanguage(LocManager.Instance.Language);
        if(current==uiLanguage)return;
        uiLanguage=current;
        TranslateControls(panel);
        ApplyArtFonts();
        RefreshTargets(true);RefreshFilterLabels();
        goldIcon.TooltipText=L("k041")+OS.GetKeycodeString((Key)prefs.Hotkey)+L("k042");
        // Refresh localized item names, retaining selected item where possible.
        foreach(var browser in browsers){var selected=browser.Selected;RefreshBrowser(browser);if(selected!=null){int index=browser.Items.FindIndex(m=>m.Id==selected.Id);if(index>=0){browser.Selected=browser.Items[index];browser.List.Select(index);browser.Detail.Text=ItemTitle(browser.Selected)+"\n"+browser.Selected.Id.Entry;}}}
        RefreshManagerLanguage();
        GD.Print("[HostGold] UI language refreshed: "+LocManager.Instance.Language);
    }
    static void RefreshManagerLanguage(){
        // Re-register stable keys, suppressing storage callbacks to preserve current preferences.
        registering=true;
        try{
            if(configApi!=null)RegisterModConfig();
            if(jmcApi!=null)TryIntegrationRefresh(RegisterJmc);
            if(ritsuAssemblyMarker!=null)TryIntegrationRefresh(RegisterRitsu);
        }finally{registering=false;}
        foreach(string key in SettingKeys)BroadcastSetting(key);
        integrationLabel.Text=integrations.Count==0?L("k043"):L("k044")+string.Join(" / ",integrations);
    }
    static void TryIntegrationRefresh(Func<bool> register){try{register();}catch(Exception e){GD.PrintErr("[HostGold] Language integration refresh: "+e.Message);}}
    static void ResetToolState(){
        if(busy){resultLabel.Text=L("k045");return;}
        resizeSide=resizeVertical=0;dragTarget=null;dragMoved=false;capturing=false;keyDown=true;last=DateTime.MinValue;
        RefreshBrowsers();UpdateBrowserButtons(Check(out _)==null);
        resultLabel.Text=L("k046");
    }
}
