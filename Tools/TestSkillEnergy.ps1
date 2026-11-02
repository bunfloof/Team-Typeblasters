$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
function Member($s,$sig){$a=$s.IndexOf($sig);if($a-lt 0){throw $sig};$b=$s.IndexOf('{',$a);$d=1;$e=$b+1;while($d-gt 0){if($s[$e]-eq'{'){$d++};if($s[$e]-eq'}'){$d--};$e++};$s.Substring($a,$e-$a)}
$p=Get-Content Assets/Scripts/Powers.cs -Raw;$g=Get-Content Assets/Scripts/GameManager.cs -Raw
$costs=foreach($name in 'LureCost','FreezeCost','FrenzyCost','RocketCost'){[regex]::Match($p,"public const int $name = [^;]+;").Value}
$power=foreach($sig in 'public void WeaponKeyDown(','public void WeaponKeyUp(','private void UseWeapon(bool rocket, string via)','public void TryUseFreeze(','public bool AddCharge(','private static void LogUseRefused(','public bool CanUseNow(','public void OnWaveStart(','public bool ConsumeRocket(','private static AnalyticsEvent PowerEvent(','private static void SendPowerEvent('){Member $p $sig}
$game=foreach($sig in 'public bool TrySpendSkillEnergy(','public bool AddSkillEnergy(','public void GrantTrainingEnergy(','public int AddKills(','public void OnWrongKey(','public void LoseComboStep(','public void ResetCombo('){Member $g $sig}
$program=@'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {public static class Mathf {public static int Max(int a,int b){return Math.Max(a,b);}}}
public enum GameState {Start,Playing,Paused,Won,Lost}
public class EasyTutorial {public static EasyTutorial Active;public bool IsDemonstrating;public bool IsFinalWave;public bool AllowsPower(string s){return true;}}
public enum PowerKind {Lure,Freeze}
public class AnalyticsEvent {public Dictionary<string,object> Values=new Dictionary<string,object>();public void Add(string k,object v){Values[k]=v;}}
public static class Analytics {public static List<AnalyticsEvent> Sent=new List<AnalyticsEvent>();public static AnalyticsEvent NewEvent(string n){return new AnalyticsEvent();}public static void AddPlayerState(AnalyticsEvent e){}public static void Send(AnalyticsEvent e){Sent.Add(e);}}
public class RunStats {public int Kills,BestCombo,BiggestMultiKill,WrongKeys;}
public class HUD {public string Hint="";public void ShowHint(string s,float n){Hint=s;}public void ShowBigMessage(string a,string b){}public void SetCombo(int n){}public void SetScore(int n){}public void ShowMultiKill(int n,int p){}public void PulseWeapon(bool r){}public void PulsePower(PowerKind k){}}
public class GameManager {
 public static GameManager Instance;public GameState State=GameState.Playing;public int Combo=7;public int SkillEnergy{get;private set;}public int Score;const int PointsPerKill=10;
 public RunStats Stats=new RunStats();public HUD hud=new HUD();public Powers powers;public Powers Powers=>powers;
GAME
}
public class Powers {
 CONSTANTS
 public bool RocketLoaded;public static bool IsFrenzy;public HUD hud;
 public static float freezeTimeLeft;float freezeSeconds=4f;public static bool IsFrozen=>freezeTimeLeft>0f;private void RefreshHud(){}
 public static event Action<string,string> ActionPerformed;
 public void OnComboChanged(int c){}private void RefreshWeaponSlots(){}private void RefreshWeaponStatus(){}private void StartFrenzy(){IsFrenzy=true;}
POWER
}
public static class SkillEnergyRegression {
 static int count;static void Check(bool c,string m){if(!c)throw new Exception(m);count++;}
 static GameManager Fresh(){Powers.IsFrenzy=false;Powers.freezeTimeLeft=0;EasyTutorial.Active=null;Analytics.Sent.Clear();var g=new GameManager();g.powers=new Powers{hud=g.hud};GameManager.Instance=g;return g;}
 public static string Run(){
  foreach(bool rocket in new[]{false,true}){
   int price=rocket?15:10;var g=Fresh();g.GrantTrainingEnergy(25);var p=g.Powers;
   Check(g.Combo==7&&g.Score==0&&g.Stats.Kills==0,"Training energy never changes combo or earned stats");
   Check(p.CanUseNow(rocket),"Enough energy lights skill");p.WeaponKeyDown(rocket);
   Check(rocket?p.RocketLoaded:Powers.IsFrenzy,"One key down activates immediately");
   Check(g.SkillEnergy==25-price&&g.Combo==7,"Only energy is spent, exact price, combo preserved");
   p.WeaponKeyUp(rocket);Check(g.SkillEnergy==25-price,"Release never repeats use");
   p.WeaponKeyDown(rocket);p.WeaponKeyUp(rocket);Check(g.SkillEnergy==25-price,"Repeated active press never spends");
   Check(!p.CanUseNow(rocket),"Active skill cannot be charged again");
   Check(Analytics.Sent.FindAll(e=>e.Values["action"].ToString()=="use").Count==1,"Exactly one actual use event");
   var used=Analytics.Sent.Find(e=>e.Values["action"].ToString()=="use");Check((int)used.Values["energy_spent"]==price&&(int)used.Values["energy_before"]==25,"Energy telemetry records exact debit");
   Check(Analytics.Sent.TrueForAll(e=>e.Values["action"].ToString()!="buy"),"No purchase event");
   if(rocket)Check(p.ConsumeRocket()&&!p.ConsumeRocket(),"Loaded rocket fires once");else Powers.IsFrenzy=false;
   g.GrantTrainingEnergy(30);p.WeaponKeyDown(rocket);Check(g.SkillEnergy==30-price,"Can use again in the same wave when energy is sufficient");
   g=Fresh();p=g.Powers;p.WeaponKeyDown(rocket);Check(g.SkillEnergy==0&&g.Combo==7&&!p.RocketLoaded&&!Powers.IsFrenzy,"Insufficient energy never activates or changes combo");
   Check(g.hud.Hint.Contains(price+" ENERGY"),"Refusal gives required energy");
   g.GrantTrainingEnergy(30);g.State=GameState.Paused;p.WeaponKeyDown(rocket);Check(g.SkillEnergy==30&&!p.RocketLoaded&&!Powers.IsFrenzy,"Paused press never spends");
  }
  {
   var g=Fresh();int score=g.AddKills(3);Check(g.SkillEnergy==3&&g.Combo==10&&g.Stats.Kills==3,"Each real kill adds one energy and retains combo progression");
   Check(score==720&&g.Score==720&&g.Stats.BestCombo==10&&g.Stats.BiggestMultiKill==3,"Existing score and multi-kill statistics unchanged");
   g.OnWrongKey();Check(g.SkillEnergy==3&&g.Combo==9&&g.Stats.WrongKeys==1,"Wrong key changes combo and accuracy only");g.ResetCombo();Check(g.Combo==1&&g.SkillEnergy==3,"Damage reset does not erase banked energy");
   EasyTutorial.Active=new EasyTutorial{IsDemonstrating=true};g.AddKills(4);Check(g.SkillEnergy==3&&g.Stats.Kills==3,"Demo kills award neither energy nor player kills");EasyTutorial.Active=null;
   Check(!g.TrySpendSkillEnergy(0)&&!g.TrySpendSkillEnergy(-1)&&!g.TrySpendSkillEnergy(4)&&g.SkillEnergy==3,"Invalid or unaffordable spends cannot mint energy");
  }
  {
   var g=Fresh();g.GrantTrainingEnergy(9);g.Powers.TryUseFreeze();Check(Powers.IsFrozen&&g.SkillEnergy==4&&g.Combo==7,"Freeze costs exactly five energy without touching combo");
   g.Powers.TryUseFreeze();Check(g.SkillEnergy==4,"Active Freeze never charges twice");Powers.freezeTimeLeft=0;g.Powers.TryUseFreeze();Check(!Powers.IsFrozen&&g.SkillEnergy==4,"Freeze refuses insufficient energy");
   g.State=GameState.Paused;g.GrantTrainingEnergy(10);g.Powers.TryUseFreeze();Check(!Powers.IsFrozen&&g.SkillEnergy==10,"Paused Freeze never spends");
   g.State=GameState.Playing;g.Powers.AddCharge(PowerKind.Lure);Check(g.SkillEnergy==14&&g.Combo==7,"Lure supply grants four independent energy");g.Powers.AddCharge(PowerKind.Freeze);Check(g.SkillEnergy==19&&g.Combo==7,"Freeze supply grants five independent energy");
  }
  return "PASS: "+count+" independent energy assertions (actual game and power methods).";
 }
}
'@
Add-Type -TypeDefinition $program.Replace('GAME',($game-join "`n")).Replace('POWER',($power-join "`n")).Replace('CONSTANTS',($costs-join "`n"))
[SkillEnergyRegression]::Run()
