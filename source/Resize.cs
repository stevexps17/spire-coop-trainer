using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
namespace HostGold;
public static partial class Entry {
    static readonly List<(Control Edge,int X,int Y)> resizeEdges=new();
    static int resizeSide,resizeVertical;
    static float resizeScale;
    static Vector2 resizeMouseStart,resizeStartSize,resizeStartPosition;
    static bool Resizing=>resizeSide!=0||resizeVertical!=0;
    static void BuildResizeEdges(Node layer){
        foreach(var direction in new[]{(-1,0),(1,0),(0,-1),(0,1),(-1,-1),(1,-1),(-1,1),(1,1)}){
            int x=direction.Item1,y=direction.Item2;
            var cursor=y==0?Control.CursorShape.Hsize:x==0?Control.CursorShape.Vsize:x==y?Control.CursorShape.Fdiagsize:Control.CursorShape.Bdiagsize;
            var edge=new Control {MouseFilter=Control.MouseFilterEnum.Stop,MouseDefaultCursorShape=cursor,Visible=false,ZIndex=x!=0&&y!=0?11:10};layer.AddChild(edge);resizeEdges.Add((edge,x,y));
            edge.GuiInput+=input=>{
                if(input is InputEventMouseButton button&&button.ButtonIndex==MouseButton.Left&&button.Pressed){
                    resizeSide=x;resizeVertical=y;resizeMouseStart=edge.GetGlobalMousePosition();resizeStartSize=panel.Size;resizeStartPosition=panel.Position;resizeScale=panel.Scale.X;dragTarget=null;edge.AcceptEvent();
                }
            };
        }
    }
    static float ResizeDimension(float start,float delta,float scale,int side,float minimum,float maximum)=>Mathf.Clamp(start+side*delta/Mathf.Max(0.01f,scale),minimum,Mathf.Max(minimum,maximum));
    static float ResizedWidth(float start,float delta,float scale,int side,float maximum)=>ResizeDimension(start,delta,scale,side,480,Mathf.Min(1000,maximum));
    static void UpdateResize(){
        if(resizeEdges.Count==0)return;
        if(Resizing){
            if(!panel.Visible||!NGame.IsGameFocusedWindow()){resizeSide=resizeVertical=0;SavePreferences();}
            else {
                var delta=panel.GetGlobalMousePosition()-resizeMouseStart;
                var far=resizeStartPosition+resizeStartSize*resizeScale;
                var screen=panel.GetViewportRect().Size;
                if(resizeSide!=0){float maximum=(resizeSide<0?far.X:screen.X-resizeStartPosition.X)/Mathf.Max(0.01f,resizeScale);prefs.PanelWidth=(int)Math.Round(ResizedWidth(resizeStartSize.X,delta.X,resizeScale,resizeSide,maximum));}
                if(resizeVertical!=0){
                    float chrome=panel.GetCombinedMinimumSize().Y-contentScroll.CustomMinimumSize.Y;
                    float maximum=(resizeVertical<0?far.Y:screen.Y-resizeStartPosition.Y)/Mathf.Max(0.01f,resizeScale);
                    prefs.PanelHeight=(int)Math.Round(ResizeDimension(resizeStartSize.Y,delta.Y,resizeScale,resizeVertical,Mathf.Max(320,chrome+100),Mathf.Min(1200,maximum)));
                }
                FitArtLayout();
                panel.Size=new Vector2(prefs.PanelWidth,resizeVertical!=0?prefs.PanelHeight:panel.Size.Y);
                panel.Position=new Vector2(resizeSide<0?far.X-panel.Size.X*resizeScale:resizeStartPosition.X,resizeVertical<0?far.Y-panel.Size.Y*resizeScale:resizeStartPosition.Y);
                if(!Input.IsMouseButtonPressed(MouseButton.Left)){resizeSide=resizeVertical=0;SavePreferences();}
            }
        }
        var size=panel.Size*panel.Scale;
        foreach(var item in resizeEdges){
            var edge=item.Edge;edge.Visible=panel.Visible;
            if(!panel.Visible)continue;
            bool corner=item.X!=0&&item.Y!=0;float grip=corner?14:9;
            edge.Position=panel.Position+new Vector2(item.X>0?size.X-grip:0,item.Y>0?size.Y-grip:0);
            edge.Size=new Vector2(item.X==0?size.X:grip,item.Y==0?size.Y:grip);
        }
    }
}
