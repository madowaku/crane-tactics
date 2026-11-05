#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using CraneTactics.Rules;
using UnityEngine;

namespace CraneTactics
{
    // Development-build evidence driver. Calls the real crane and PhysX, not Board.Resolve.
    public sealed class SmokePlayback : MonoBehaviour
    {
        OneNightGame game; string output; bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartIfRequested()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"--smoke-output");
            if(index>=0 && index+1<args.Length) {Application.runInBackground=true; var o=new GameObject("Development smoke playback"); o.AddComponent<SmokePlayback>().output=args[index+1];}
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"frames")); Time.captureFramerate=20; StartCoroutine(Frames()); yield return null;
            game=FindFirstObjectByType<OneNightGame>(); game.Automation=true;
            yield return new WaitForSeconds(1);
            yield return Capture("01-first-shift");
            yield return Drop(Kind.Heavy,new Cell(3,3));
            yield return Capture("02-heavy-impact");
            yield return Drop(Kind.Guard,game.Board.Pit); Check(game.Board.Outcome==Outcome.Victory,"Battle 1 victory");
            game.NextCabinet(); yield return new WaitForSeconds(.5f);
            yield return Drop(Kind.Slimy,new Cell(2,2)); Check(game.Board.Outcome==Outcome.Playing,"Prize miss preserved");
            yield return Capture("03-prize-miss");
            yield return Drop(Kind.Slimy,game.Board.Pit); Check(game.Board.Outcome==Outcome.Recruited,"Slimy recruited");
            yield return new WaitForSeconds(1); yield return Capture("04-team");
            game.NextCabinet(); yield return new WaitForSeconds(.5f);
            yield return Drop(Kind.Slimy,new Cell(4,3)); Check(game.Board.Units.Single(u=>u.Kind==Kind.Knight).Hp==1,"Battle 2 first turn");
            yield return Capture("05-slime-push");
            yield return Drop(Kind.Heavy,new Cell(1,1)); Check(game.Board.Outcome==Outcome.Victory,"Battle 2 victory");
            yield return new WaitForSeconds(1); yield return Capture("06-ending");
            game.NextCabinet(); Check(game.Board.Stage==0,"Replay");
            File.WriteAllText(Path.Combine(output,"result.txt"),failed?"FAILED":"PASSED: real crane / PhysX full loop. Programmatic selection; not human play or raycast evidence.");
            Application.Quit(failed?1:0);
        }
        void Check(bool valid,string description) {if(!valid) failed=true; File.AppendAllText(Path.Combine(output,"timeline.txt"),$"{Time.time:F3} {(valid?"PASS":"FAIL")} {description}\n");}
        IEnumerator Frames() {int frame=0; while(true) {yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,"frames",$"{frame++:D5}.png"));}}
        IEnumerator Capture(string name) {yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png")); yield return new WaitForSeconds(.2f);}
        IEnumerator Drop(Kind kind,Cell cell)
        {
            game.Grab(game.Board.Units.Single(u=>u.Kind==kind)); float deadline=Time.time+6;
            while(!game.IsCarrying && Time.time<deadline) yield return null;
            Check(game.IsCarrying,"Grab "+kind); game.Aim(cell); yield return new WaitForSeconds(2.2f); game.Drop(); deadline=Time.time+4;
            while(!game.IsSelecting && !game.IsResult && Time.time<deadline) yield return null;
            Check(game.IsSelecting || game.IsResult,"Settle "+kind+" at "+cell);
        }
    }
}
#endif
