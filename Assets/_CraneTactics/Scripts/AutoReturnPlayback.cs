#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    public sealed class AutoReturnPlayback : MonoBehaviour
    {
        string output; OneNightGame game; bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"--auto-output");
            if(i>=0 && i+1<args.Length) {Application.runInBackground=true; new GameObject("Auto return evidence").AddComponent<AutoReturnPlayback>().output=args[i+1];}
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"frames"));
            Time.captureFramerate=20; StartCoroutine(Frames()); yield return null;
            game=FindFirstObjectByType<OneNightGame>(); game.Automation=true; game.SetAutoReturn(true);
            yield return new WaitForSeconds(.7f); yield return Capture("01-mode-b");
            yield return Catch(Kind.Guard); yield return Catch(Kind.Scout);
            Check(game.Board.Outcome==Outcome.Victory,"B Battle 1"); game.NextCabinet(); yield return null;
            yield return Catch(Kind.Slimy); Check(game.Board.Outcome==Outcome.Recruited,"Slimy recruitment");
            game.NextCabinet(); yield return null; yield return Catch(Kind.Guard); yield return Catch(Kind.Scout);
            Check(game.Board.Outcome==Outcome.Victory,"B Battle 2"); yield return new WaitForSeconds(1); yield return Capture("02-ending");
            game.LoadExperimentCase(1); yield return null; game.Grab(game.Board.Units.Single(u=>u.Kind==Kind.Heavy));
            yield return Wait(()=>game.IsCarrying); yield return Wait(()=>game.ClawPosition.x>=3.15f);
            yield return Capture("03-release"); game.Drop(); yield return Wait(()=>game.IsPresenting);
            yield return new WaitForSeconds(.25f); yield return Capture("04a-push-moving");
            yield return new WaitForSeconds(.3f); yield return Capture("04b-chute-fall");
            yield return Wait(()=>game.IsSelecting || game.IsResult);
            Check(game.Board.Units.Single(u=>u.Kind==Kind.Guard).Hp<3,"A enemy impact");
            Check(game.Board.Outcome==Outcome.Victory && game.Board.Turns==1,"A early drop combo wins in one action"); yield return Capture("04-impact");
            game.LoadExperimentCase(1); yield return null; yield return Catch(Kind.Heavy); yield return Catch(Kind.Guard);
            Check(game.Board.Outcome==Outcome.Victory && game.Board.Turns==2,"A direct captures take two actions");
            game.LoadExperimentCase(2); yield return null; yield return Catch(Kind.Guard);
            Check(game.Board.Outcome==Outcome.Victory && game.Board.Turns==1,"B near-home direct capture wins in one action");
            game.LoadExperimentCase(3); yield return null; game.BeginAutoGrab(new Vector3(3.55f,0,2));
            yield return Wait(()=>game.IsSelecting || game.IsResult); Check(game.Board.Captured.Count==1,"C failed grab captures by push"); yield return Capture("05-contact-push");
            game.ExportExperiment(Path.Combine(output,"input-results.jsonl"));
            File.WriteAllText(Path.Combine(output,"result.txt"),failed?"FAILED":"PASSED: automated Unity PhysX; not human playtest.");
            yield return new WaitForSeconds(.3f); Application.Quit(failed?1:0);
        }
        IEnumerator Catch(Kind kind) {game.Grab(game.Board.Units.Single(u=>u.Kind==kind)); yield return Wait(()=>game.IsCarrying); yield return Wait(()=>game.IsSelecting || game.IsResult);}
        IEnumerator Wait(Func<bool> condition) {float end=Time.time+12; while(!condition() && Time.time<end) yield return null; Check(condition(),"state reached");}
        void Check(bool value,string label) {if(!value) failed=true; File.AppendAllText(Path.Combine(output,"timeline.txt"),$"{Time.time:F3} {(value?"PASS":"FAIL")} {label}\n");}
        IEnumerator Frames() {int frame=0; while(true) {yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,"frames",$"{frame++:D5}.png"));}}
        IEnumerator Capture(string label) {yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,label+".png")); File.AppendAllText(Path.Combine(output,"media-times.jsonl"),$"{{\"path\":\"{label}.png\",\"at_ms\":{Mathf.RoundToInt(Time.time*1000)}}}\n"); yield return new WaitForSeconds(.1f);}
    }
}
#endif
