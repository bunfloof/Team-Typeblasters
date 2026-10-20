$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
# Exercise the actual merged methods outside Unity. Only engine/UI services
# are replaced; this does not claim to replace a Unity or mobile play test.
function ReadSource($name) { [IO.File]::ReadAllText((Join-Path $PWD "Assets/Scripts/$name.cs")) }
function Member($source,$signature) {
    $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw "Missing member $signature"}
    $open=$source.IndexOf('{',$start);$depth=1;$end=$open+1
    while($depth -gt 0 -and $end -lt $source.Length){
        if($source[$end] -eq '{'){$depth++}
        if($source[$end] -eq '}'){$depth--}
        $end++
    }
    if($depth -ne 0){throw "Unbalanced member $signature"}
    return $source.Substring($start,$end-$start)
}
$game=ReadSource 'GameManager';$powers=ReadSource 'Powers';$zombie=ReadSource 'Zombie'
$touch=ReadSource 'TouchControls';$typing=ReadSource 'TypingController'
$gameMethod=Member $game 'public void TakeDamage('
$bite=Member $zombie 'private int BiteDamage'
$native=Member $touch 'public void OnNativeText('
$space=Member $typing 'private bool NextIsSpace('
$powerMethods=foreach($signature in 'public static bool IsShielded','public static float EnemySpeedFactor','public void TryUseShield(','private void CloseShieldWord(','public void RaiseShield(','public void OnShieldHit(','private void UpdateShield(','private void RefreshShieldHud(','private static AnalyticsEvent PowerEvent(','private static void SendPowerEvent('){Member $powers $signature}
$settings=[regex]::Replace((ReadSource 'TypingSettings'),'(?m)^using [^;]+;\r?\n','')
$event=[regex]::Replace((ReadSource 'AnalyticsEvent'),'(?m)^using [^;]+;\r?\n','')
$header=@'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
'@
$stubs=@'
namespace UnityEngine {
 public struct Color { public Color(float r,float g,float b,float a=1f){} }
 public static class Mathf {
  public static int Clamp(int v,int a,int b){return Math.Min(b,Math.Max(a,v));}
  public static float Max(float a,float b){return Math.Max(a,b);}
  public static int Max(int a,int b){return Math.Max(a,b);}
 }
 public static class Random { static System.Random rng=new System.Random(526); public static float value {get{return (float)rng.NextDouble();}} public static int Range(int a,int b){return rng.Next(a,b);} }
 public static class PlayerPrefs {static int saved; public static int GetInt(string key,int fallback){return saved;} public static void SetInt(string key,int v){saved=v;} public static void Save(){} }
 public static class Time {public static float deltaTime;}
 public static class Object {public static T FindFirstObjectByType<T>() where T:class{return null;} }
}
public enum GameState {Start,Playing,Paused,Tip,Won,Lost}
public enum ZombieKind {Normal,Brute,Ghost,Splitling}
public class HUD {
 public int Flashes; public void SetHealth(int h,int max){} public void FlashRed(){Flashes++;}
 public void ShowHint(string text,float seconds){} public void SetShieldPromptTime(float v){}
 public void PulseShield(){} public void SetShield(int n,int max,float f){}
}
public static class CameraDirector {public static void Shake(float n){} }
public static class Analytics {
 public static List<string> Sent=new List<string>();
 public static AnalyticsEvent NewEvent(string name){return new AnalyticsEvent(name);}
 public static void Send(AnalyticsEvent e){Sent.Add(e.ToJson());}
 public static void AddPlayerState(AnalyticsEvent e,Zombie ignore=null){e.Add("health",GameManager.Instance.Health);e.Add("combo",GameManager.Instance.Combo);}
}
public class WaveSpawner {public List<char> UsedFirstLetters(){return new List<char>();}}
public static class WordBank {public static int CompoundSplit(string w){return 0;} public static string PickShieldWord(List<char> used){return "PROTECT";}}
public class ShieldWord {
 public static ShieldWord Current; public static void Open(string w,Powers p,HUD h){Current=new ShieldWord();}
 public static void Close(){Current=null;}
}
public static class TouchInput {public static StringBuilder Text=new StringBuilder();public static void Type(char c){Text.Append(c);}}
public interface ITypingTarget {bool IsAlive{get;}bool IsWordComplete{get;}char NextLetter{get;}}
public class Target : ITypingTarget {public bool IsAlive{get;set;}=true;public bool IsWordComplete{get;set;}public char NextLetter{get;set;}}
'@
$classes=@"
public class GameManager {
 public static GameManager Instance;
 public GameState State=GameState.Playing;public int Health=100;public int Combo=7;private int startingHealth=100;
 public HUD hud=new HUD();public Powers powers=new Powers();public Powers Powers {get{return powers;}}
 private Zombie fatalAttacker; public void LoseComboStep(){Combo--;} public void ResetCombo(){Combo=1;}
 public void EndGame(bool won){State=won?GameState.Won:GameState.Lost;}
 $gameMethod
}
public class Powers {
 public static float shieldTimeLeft;private static float shieldSlow=1f;
 public int ShieldCharges=1;private int shieldChargesPerLevel=1;private float shieldSeconds=8f;
 private float shieldWordTimeLeft;private float shieldWordSeconds=6f;private float shieldEnemySpeed=.35f;
 private HUD hud=new HUD();
 private static void LogUseRefused(string item,string reason,string via){}
 public void Tick(float dt){Time.deltaTime=dt;UpdateShield();}
 $($powerMethods -join "`n")
}
public class Zombie {public ZombieKind Kind;public int damagePerHit=10;public int Damage {get{return BiteDamage;}} $bite }
public class TouchControls {$native}
public class TypingController {private List<Target> candidates=new List<Target>();public bool AcceptsSpace(Target t){candidates.Clear();candidates.Add(t);return NextIsSpace();} $space}
"@
$tests=@'
public static class MergeRegression {
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static GameManager Fresh(){Powers.shieldTimeLeft=0;ShieldWord.Current=null;Analytics.Sent.Clear();return GameManager.Instance=new GameManager();}
 public static string Run(){
  var game=Fresh();game.TakeDamage(10,false,new AnalyticsEvent("damage"),null);
  Check(game.Health==90 && game.Combo==1,"Normal hit health/combo");
  Check(Analytics.Sent[0].Contains("\"health\":100") && Analytics.Sent[0].Contains("\"health_after\":90"),"Pre/post damage statistics");
  game=Fresh();game.TakeDamage(10,true,new AnalyticsEvent("damage"),null);
  Check(game.Health==90 && game.Combo==6,"Ambush only costs one combo");
  game=Fresh();game.TakeDamage(100,false,new AnalyticsEvent("damage"),null);
  Check(game.State==GameState.Lost && Analytics.Sent[0].Contains("\"fatal\":true"),"Fatal damage statistics");
  game=Fresh();game.powers.TryUseShield();
  Check(ShieldWord.Current!=null && game.powers.ShieldCharges==1,"Opening shield spends no charge");
  game.powers.TryUseShield();Check(ShieldWord.Current==null && game.powers.ShieldCharges==1,"Cancelling keeps charge");
  game.powers.TryUseShield();game.powers.Tick(7f);
  Check(ShieldWord.Current==null && game.powers.ShieldCharges==1,"Shield timeout keeps charge");
  game.powers.TryUseShield();game.State=GameState.Paused;game.powers.Tick(10f);
  Check(ShieldWord.Current!=null,"Paused shield word does not time out");
  game.State=GameState.Playing;ShieldWord.Close();game.powers.RaiseShield();
  Check(Powers.IsShielded && game.powers.ShieldCharges==0 && Math.Abs(Powers.EnemySpeedFactor-.35f)<.001f,"Successful shield spends one charge and slows enemies");
  game.TakeDamage(20,false,new AnalyticsEvent("damage"),new Zombie());
  Check(game.Health==100 && game.Combo==7 && game.hud.Flashes==0,"Shield blocks health/combo/flash changes");
  string blocked=Analytics.Sent[Analytics.Sent.Count-1];
  Check(blocked.Contains("\"amount\":0") && blocked.Contains("\"attempted_amount\":20") && blocked.Contains("\"shield_blocked\":true"),"Blocked attacks keep zero-damage telemetry");
  game.State=GameState.Paused;game.powers.Tick(10f);Check(Powers.IsShielded,"Paused shield does not expire");
  game.State=GameState.Playing;game.powers.Tick(9f);Check(!Powers.IsShielded && Powers.EnemySpeedFactor==1f,"Shield expires and restores speed");
  game.powers.TryUseShield();Check(ShieldWord.Current==null,"No second shield after charge spent");
  game=Fresh();game.State=GameState.Start;game.TakeDamage(50,false,new AnalyticsEvent("damage"),null);
  Check(game.Health==100 && Analytics.Sent.Count==0,"No damage outside play");
  var z=new Zombie();z.Kind=ZombieKind.Brute;Check(z.Damage==20,"Brute double damage");z.Kind=ZombieKind.Ghost;Check(z.Damage==5,"Ghost half damage");z.Kind=ZombieKind.Normal;Check(z.Damage==10,"Normal damage retained");
  new TouchControls().OnNativeText("Cold Dark! 0567891234\n");
  Check(TouchInput.Text.ToString()=="Cold Dark! 056789","Mobile preserves spaces, case and symbols; reserves power digits");
  var typing=new TypingController();var target=new Target{NextLetter=' '};Check(typing.AcceptsSpace(target),"Phrase accepts space");target.IsWordComplete=true;Check(!typing.AcceptsSpace(target),"Completed words cannot consume space");
  foreach(TypingDifficulty difficulty in Enum.GetValues(typeof(TypingDifficulty))){
   TypingSettings.Difficulty=difficulty;
   Check(TypingSettings.IsCaseSensitive==(difficulty!=TypingDifficulty.Easy),"Difficulty controls case sensitivity");
   Check(TypingSettings.HasShifters==(difficulty>=TypingDifficulty.Hard),"Difficulty controls shifters independently of map");
   for(int i=0;i<200;i++){
    string word=TypingSettings.Style("WATER");
    Check(word.IndexOfAny("1234<>".ToCharArray())<0,"Styled words avoid power keys and rich text delimiters");
    if(difficulty==TypingDifficulty.Easy)Check(TypingSettings.Shown(word,false)=="water","Easy displays lowercase");
    if(difficulty==TypingDifficulty.Medium)Check(word.ToUpperInvariant()=="WATER","Medium preserves spelling without symbols");
   }
  }
  return "PASS: "+count+" assertions (damage/telemetry, shield lifecycle/pause, mobile phrases, four typing difficulties).";
 }
}
'@
Add-Type -TypeDefinition ($header+$stubs+$event+$settings+$classes+$tests)
[MergeRegression]::Run()
# Source-level wiring checks supplement behavior tests without loading Unity UI.
foreach($snippet in 'TouchPowerButton.Power.Shield','float total = PauseWeight + 5f + TypeWeight') {if(!$touch.Contains($snippet)){throw "Missing mobile shield wiring: $snippet"}}
$button=ReadSource 'TouchPowerButton'
if(!$button.Contains('TouchInput.Press(Key.Tab)') -or !$button.Contains('TouchInput.Release(Key.Tab)')){throw 'Mobile shield does not use desktop Tab lifecycle'}
if((ReadSource 'Easy/EasyShield') -match 'private void Update\('){throw 'Legacy shield still processes input'}
Write-Output 'PASS: native mobile SHIELD button uses Tab; legacy shield has no input loop.'
