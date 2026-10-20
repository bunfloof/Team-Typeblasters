$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$source=Get-Content Assets/Scripts/Powers.cs -Raw
$hudSource=Get-Content Assets/Scripts/HUD.cs -Raw
function Member($text,$signature) {
    $start=$text.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw "Missing $signature"}
    $open=$text.IndexOf('{',$start); $depth=1; $end=$open+1
    while($depth -gt 0){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
    $text.Substring($start,$end-$start)
}
$methods=foreach($signature in 'public void WeaponKeyDown(','public void WeaponKeyUp(','private void UpdateWeaponHolds(','private void TapWeapon(','private int Kept(','private void BuyWeapon(','private void UseWeapon(bool rocket, string via)','private static void LogUseRefused(','private bool CanBuyWeapon(','public bool CanBuyNow(','public void OnWaveStart(','public bool ConsumeRocket(','private static AnalyticsEvent PowerEvent(','private static void SendPowerEvent('){Member $source $signature}
$active=Member $hudSource 'private static bool IsPowerRowActive('
$constants=foreach($name in 'BuyHoldSeconds','FrenzyCost','RocketCost','MaxKept','HoldBarDelay') {
    [regex]::Match($source,"(?:public|private) const (?:int|float) $name = [^;]+;").Value
}
$program=@'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public static class Mathf {public static float Clamp01(float n){return Math.Clamp(n,0f,1f);}}
 public static class Time {public static float deltaTime;}
}
public enum GameState {Playing,Paused}
public class AnalyticsEvent {public Dictionary<string,object> Values=new Dictionary<string,object>();public void Add(string k,object v){Values[k]=v;}}
public static class Analytics {
 public static List<AnalyticsEvent> Sent=new List<AnalyticsEvent>();
 public static AnalyticsEvent NewEvent(string name){return new AnalyticsEvent();}
 public static void AddPlayerState(AnalyticsEvent e){}
 public static void Send(AnalyticsEvent e){Sent.Add(e);}
}
public class GameManager {
 public static GameManager Instance;public Powers Powers;public GameState State=GameState.Playing;public int Combo=50;
 public void SpendCombo(int amount){Combo-=amount;Powers.SetCombo(Combo);}
}
public class HUD {
 public string Hint="";public string Message="";const int FrenzyRow=2;
 public void ShowHint(string text,float seconds){Hint=text;}
 public void SetWeaponHold(bool rocket,float progress){}
 public void PulseWeapon(bool rocket){} public void ShowWeaponGain(bool rocket){}
 public void ShowBigMessage(string title,string text){Message=title;}
 public static bool Active(int row){return IsPowerRowActive(row);}
ACTIVE_METHOD
}
public class Powers {
CONSTANTS
 public static event Action<string,string> ActionPerformed;
 public bool RocketLoaded; public int RocketKept;public int FrenzyKept;
 public static bool IsFrenzy;public static bool IsFrozen;public bool IsAiming;
 private bool oneWeaponPerWave=true;private bool weaponBoughtThisWave;private int combo=50;
 private readonly float[] weaponHold={-1f,-1f};public HUD hud=new HUD();
 public void SetCombo(int n){combo=n;} public void Tick(float seconds){Time.deltaTime=seconds;UpdateWeaponHolds();}
 public void Use(bool rocket){UseWeapon(rocket,"test");}
 private void RefreshWeaponSlots(){}private void RefreshWeaponStatus(){}
 private void StartFrenzy(){IsFrenzy=true;hud.ShowBigMessage("FRENZY!","");}
METHODS
}
public static class PowerControlRegression {
 static int count;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
 static Powers Fresh(){Powers.IsFrenzy=false;Powers.IsFrozen=false;var p=new Powers();GameManager.Instance=new GameManager{Powers=p};Analytics.Sent.Clear();return p;}
 public static string Run(){
  foreach(bool rocket in new[]{false,true}){
   foreach(float held in new[]{0f,.1f,.3f,.45f,.59f}){
    var p=Fresh();if(rocket)p.RocketKept=1;else p.FrenzyKept=1;
    p.WeaponKeyDown(rocket);Check(p.hud.Hint.Contains("release to USE"),"Key-down explains both actions");
    p.Tick(held);p.WeaponKeyUp(rocket);
    Check(rocket?p.RocketLoaded:Powers.IsFrenzy,"Every release before threshold uses inventory, including old dead zone");
    Check(GameManager.Instance.Combo==50,"Using stored weapon does not spend combo");
    Check(HUD.Active(rocket?3:2),"Used weapon stays highlighted with zero stored charges");
    p.Use(rocket);Check(p.hud.Hint.Contains(rocket?"LOADED":"ACTIVE"),"Repeated use clearly reports existing effect");
   }
   foreach(float held in new[]{.6f,.9f}){
    var p=Fresh();p.WeaponKeyDown(rocket);p.Tick(held);p.WeaponKeyUp(rocket);
    Check((rocket?p.RocketKept:p.FrenzyKept)==1,"Full hold buys exactly once");
    Check(!p.RocketLoaded&&!Powers.IsFrenzy,"Releasing after purchase does not consume it");
    Check(GameManager.Instance.Combo==50-(rocket?Powers.RocketCost:Powers.FrenzyCost),"Purchase spends exact price");
   }
   {
    var p=Fresh();p.WeaponKeyDown(rocket);p.Tick(.45f);p.WeaponKeyUp(rocket);
    Check(p.hud.Hint.Contains("BUY")&&p.hud.Hint.Contains("0.6"),"Empty short press explains hold requirement");
    Check(GameManager.Instance.Combo==50,"Empty short press spends nothing");
   }
   {
    var p=Fresh();p.SetCombo(1);GameManager.Instance.Combo=1;p.WeaponKeyDown(rocket);p.Tick(.7f);p.WeaponKeyUp(rocket);
    Check(p.hud.Hint.Contains("needs")&&GameManager.Instance.Combo==1,"Insufficient combo produces clear refusal without spending");
   }
   {
    var p=Fresh();if(rocket)p.RocketKept=1;else p.FrenzyKept=1;
    p.WeaponKeyDown(rocket);GameManager.Instance.State=GameState.Paused;p.Tick(.7f);p.WeaponKeyUp(rocket);
    Check((rocket?p.RocketKept:p.FrenzyKept)==1&&!p.RocketLoaded&&!Powers.IsFrenzy,"Pause cancels hold without consuming weapon");
   }
  }
  {
   var p=Fresh();p.RocketKept=1;p.Use(true);Check(p.ConsumeRocket()&&!p.ConsumeRocket(),"Loaded rocket fires once");
   Check(!HUD.Active(3),"RPG highlight clears after firing");
   Powers.IsFrozen=true;Check(HUD.Active(1),"Freeze highlights while active without charges");
   Powers.IsFrozen=false;Check(!HUD.Active(1),"Freeze highlight clears on expiry");
  }
  return "PASS: "+count+" power control assertions (tap boundary, holds, costs, active effects, empty/refused inputs, pause and highlight predicates).";
 }
}
'@
$program=$program.Replace('ACTIVE_METHOD',$active).Replace('CONSTANTS',($constants -join "`n")).Replace('METHODS',($methods -join "`n"))
Add-Type -TypeDefinition $program
[PowerControlRegression]::Run()
