using System;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.Fonts;
namespace HostGold;
public static partial class Entry {
    static Theme spireTheme;
    static Label windowTitle;
    static ScrollContainer contentScroll;
    static readonly Color Ink=new Color("201d24"),Gold=new Color("b89358"),Paper=new Color("f4e6c9"),Muted=new Color("b8ac98");
    static StyleBoxFlat Surface(string fill,string edge,int padding=12,int border=1,int radius=5){
        return new StyleBoxFlat {BgColor=new Color(fill),BorderColor=new Color(edge),BorderWidthLeft=border,BorderWidthRight=border,BorderWidthTop=border,BorderWidthBottom=border,CornerRadiusTopLeft=radius,CornerRadiusTopRight=radius,CornerRadiusBottomLeft=radius,CornerRadiusBottomRight=radius,ContentMarginLeft=padding,ContentMarginRight=padding,ContentMarginTop=padding,ContentMarginBottom=padding};
    }
    static Font GameFont(bool bold){
        try{
            string lang=LocManager.Instance.Language;
            var font=FontManager.GetSubstituteFont(lang,bold?FontType.Bold:FontType.Regular);
            if(font!=null)return font;
            string path=bold?"res://themes/kreon_bold_shared.tres":"res://themes/kreon_regular_shared.tres";
            return ResourceLoader.Exists(path)?ResourceLoader.Load<Font>(path):null;
        }catch(Exception e){GD.PrintErr("[HostGold] Font fallback: "+e.Message);return null;}
    }
    static Theme CreateSpireTheme(){
        var theme=new Theme {DefaultFontSize=18};var font=GameFont(false);if(font!=null)theme.DefaultFont=font;
        foreach(string type in new[]{"Label","Button","CheckBox","CheckButton","OptionButton","LineEdit","ItemList","PopupMenu","TabContainer","TabBar"}){
            theme.SetColor("font_color",type,Paper);theme.SetColor("font_hover_color",type,new Color("fff2d3"));theme.SetColor("font_pressed_color",type,new Color("ffe2a0"));theme.SetColor("font_focus_color",type,Paper);theme.SetColor("font_disabled_color",type,new Color("776f67"));
            theme.SetColor("font_outline_color",type,new Color("100e12"));theme.SetConstant("outline_size",type,2);
        }
        foreach(string type in new[]{"Button","OptionButton","CheckBox","CheckButton"}){
            theme.SetStylebox("normal",type,Surface("302a2b","675440",10));theme.SetStylebox("hover",type,Surface("43372c","c39b60",10));theme.SetStylebox("pressed",type,Surface("51402c","dfb96e",10));theme.SetStylebox("hover_pressed",type,Surface("51402c","dfb96e",10));theme.SetStylebox("disabled",type,Surface("242125","49413b",10));
            var focus=Surface("00000000","e3c181",10,2);focus.DrawCenter=false;theme.SetStylebox("focus",type,focus);
        }
        theme.SetTypeVariation("HostPrimary","Button");theme.SetStylebox("normal","HostPrimary",Surface("65502f","c8a462",12,2));theme.SetStylebox("hover","HostPrimary",Surface("80633a","f2d08a",12,2));theme.SetStylebox("pressed","HostPrimary",Surface("483a27","d0ac6a",12,2));
        theme.SetStylebox("normal","LineEdit",Surface("16151b","66513c",10));theme.SetStylebox("focus","LineEdit",Surface("1f1c22","d1ad6d",10,2));theme.SetStylebox("read_only","LineEdit",Surface("1b191e","49413b",10));theme.SetColor("caret_color","LineEdit",Paper);theme.SetColor("selection_color","LineEdit",new Color("70583d"));
        theme.SetStylebox("panel","ItemList",Surface("17161c","66513c",8));theme.SetStylebox("selected","ItemList",Surface("57442d","b58e54",6));theme.SetStylebox("selected_focus","ItemList",Surface("665032","e5bc72",6));theme.SetStylebox("hovered","ItemList",Surface("332c26","67533d",6));theme.SetColor("font_selected_color","ItemList",new Color("fff0cc"));theme.SetConstant("v_separation","ItemList",10);
        theme.SetStylebox("panel","TabContainer",Surface("242027","6e573e",14));theme.SetStylebox("tab_selected","TabContainer",Surface("51402e","c19a5d",12));theme.SetStylebox("tab_unselected","TabContainer",Surface("282329","534536",12));theme.SetStylebox("tab_hovered","TabContainer",Surface("3b3029","ae8852",12));theme.SetColor("font_selected_color","TabContainer",Paper);theme.SetColor("font_unselected_color","TabContainer",Muted);theme.SetFontSize("font_size","TabContainer",19);
        theme.SetStylebox("panel","PopupMenu",Surface("252128","ac8650",10));theme.SetStylebox("hover","PopupMenu",Surface("57442e","ac8650",8));theme.SetConstant("v_separation","PopupMenu",12);
        theme.SetStylebox("scroll","VScrollBar",Surface("19171b","302a29",3));theme.SetStylebox("grabber","VScrollBar",Surface("8c7049","b99761",4));theme.SetStylebox("grabber_highlight","VScrollBar",Surface("b28d56","dbb779",4));theme.SetStylebox("grabber_pressed","VScrollBar",Surface("c49c60","e2c185",4));
        theme.SetStylebox("separator","HSeparator",Surface("00000000","69533b",0,1,0));
        return theme;
    }
    static VBoxContainer Section(VBoxContainer parent){var card=new PanelContainer();card.AddThemeStyleboxOverride("panel",Surface("29242b","69543c",12));parent.AddChild(card);var inner=new VBoxContainer();inner.AddThemeConstantOverride("separation",8);card.AddChild(inner);return inner;}
    static void MutedLabel(Label label){label.AddThemeColorOverride("font_color",Muted);label.AddThemeFontSizeOverride("font_size",15);label.AutowrapMode=TextServer.AutowrapMode.WordSmart;label.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;}
    static void ApplyArtFonts(){
        if(spireTheme==null)return;var font=GameFont(false);if(font!=null)spireTheme.DefaultFont=font;
        var bold=GameFont(true);if(windowTitle!=null&&bold!=null)windowTitle.AddThemeFontOverride("font",bold);
    }
    static float CalculateUiScale(float width,float height,float panelWidth,float panelHeight,int percent){
        if(width<=0||height<=0||panelWidth<=0||panelHeight<=0)return 1f;
        float requested=Math.Clamp(percent,50,150)/100f;
        float automatic=Mathf.Min(1f,Mathf.Min(width/1600f,height/900f));
        float fit=Mathf.Min(Mathf.Max(1,width-24)/panelWidth,Mathf.Max(1,height-24)/panelHeight);
        return Mathf.Min(requested*automatic,fit);
    }
    static void FitArtLayout(){
        if(contentScroll==null||panel==null)return;
        // Keep layout units stable; scale the entire panel, including fonts and hit targets.
        float bodyHeight=contentScroll.GetChild<Control>(0).GetCombinedMinimumSize().Y;
        float chrome=panel.GetCombinedMinimumSize().Y-contentScroll.CustomMinimumSize.Y;
        float wantedHeight=prefs.PanelHeight>0?Mathf.Max(100,prefs.PanelHeight-chrome):Mathf.Clamp(bodyHeight,100,460);
        if(Mathf.Abs(contentScroll.CustomMinimumSize.Y-wantedHeight)>1||panel.CustomMinimumSize.X!=prefs.PanelWidth){
            contentScroll.CustomMinimumSize=new Vector2(0,wantedHeight);panel.CustomMinimumSize=new Vector2(prefs.PanelWidth,0);panel.Size=new Vector2(prefs.PanelWidth,0);
        }
        var viewport=panel.GetViewportRect().Size;
        var size=panel.Size.Max(panel.GetCombinedMinimumSize());
        float scale=Resizing?resizeScale:CalculateUiScale(viewport.X,viewport.Y,size.X,size.Y,prefs.UiScale);
        if(Mathf.Abs(panel.Scale.X-scale)>0.001f){
            panel.Scale=new Vector2(scale,scale);
            foreach(var browser in browsers)if(browser.Filter!=null)browser.Filter.GetPopup().ContentScaleFactor=scale;
        }
        panel.Position=KeepOnScreen(panel,panel.Position);
    }
    static void AddCoinArt(Panel coin){
        try {const string path="res://images/packed/sprite_fonts/gold_icon.png";if(!ResourceLoader.Exists(path))return;
            var tex=ResourceLoader.Load<Texture2D>(path);if(tex==null)return;
            var icon=new TextureRect {Texture=tex,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=Control.MouseFilterEnum.Ignore};coin.AddChild(icon);icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);icon.OffsetLeft=6;icon.OffsetTop=6;icon.OffsetRight=-6;icon.OffsetBottom=-6;iconGlyph.Hide();
        }catch(Exception e){GD.PrintErr("[HostGold] Icon fallback: "+e.Message);}
    }
}
