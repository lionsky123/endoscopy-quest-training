using System;
using Endoscopy;

static class LayoutChecks
{
    static int assertions;
    static void Require(bool condition,string message)
    { assertions++; if(!condition)throw new Exception(message); }
    static string Subject(LayoutCheck check, LayoutFinding fault)
    {
        switch(check)
        {
            case LayoutCheck.Door:return "door";
            case LayoutCheck.Partition:return "partition";
            case LayoutCheck.Sinks:return "sink-digest";
            case LayoutCheck.Machines:return "machine-digest";
            case LayoutCheck.Stages:return "rinse-place";
            default:return fault==LayoutFinding.ReturnFlow?"flow-return":"flow-forward";
        }
    }
    public static void Main()
    {
        var handle=new DoorHandleGate();
        Require(!handle.Sample(true,0,false),"Handle must first be approached");
        handle.Sample(true,.1f,false);Require(handle.Sample(true,.01f,false),"Fresh near touch opens door");
        Require(!handle.Sample(true,.01f,false),"Holding contact cannot toggle repeatedly");
        handle.Sample(true,.1f,true);Require(!handle.Sample(true,.01f,false),"Moving handle cannot rearm beneath a stationary finger");
        handle.Sample(true,.1f,false);handle.Sample(false,0,false);Require(!handle.Sample(true,.01f,false),"Tracking loss resets handle gate");
        handle.Sample(true,.1f,false);Require(handle.Sample(true,.01f,false),"Retreat and reapproach rearm handle");
        foreach(LayoutFinding fault in Enum.GetValues(typeof(LayoutFinding)))
        {
            var scene=new LayoutCase(fault);
            foreach(LayoutCheck check in Enum.GetValues(typeof(LayoutCheck)))
            {
                var expected=scene.Expected(check);var target=Subject(check,fault);
                Require(scene.Evaluate(check,target,expected),"Correct visible evidence was rejected");
                Require(!scene.Evaluate(check,"unrelated-object",expected),"Unrelated object must not pass");
                var wrong=expected==LayoutFinding.Compliant?(LayoutFinding)((int)check+1):LayoutFinding.Compliant;
                Require(!scene.Evaluate(check,target,wrong),"Wrong finding must not pass");
            }
        }
        Require(!new LayoutCase(LayoutFinding.SharedSink).Evaluate(LayoutCheck.Sinks,"sink-respiratory",LayoutFinding.SharedSink),"Point at shared sink, not missing counterpart");
        Require(!new LayoutCase(LayoutFinding.SharedMachine).Evaluate(LayoutCheck.Machines,"machine-respiratory",LayoutFinding.SharedMachine),"Point at shared machine, not missing counterpart");
        Require(!new LayoutCase(LayoutFinding.ReturnFlow).Evaluate(LayoutCheck.Flow,"flow-forward",LayoutFinding.ReturnFlow),"Return flow requires the returning segment");
        Require(new LayoutCase(LayoutFinding.MissingRinse).Expected(LayoutCheck.Flow)==LayoutFinding.Compliant,"Missing stage and reversal are separate findings");
        for(int topic=0;topic<3;topic++)
        {
            var attempt=new LayoutAttempt();var scene=new LayoutCase(LayoutFinding.Compliant);
            var first=(LayoutCheck)(topic*2);var second=(LayoutCheck)(topic*2+1);
            attempt.Begin(topic,scene);
            Require(!attempt.Complete&&!attempt.Independent,"Reading/starting must not complete");
            Require(!attempt.Submit((LayoutCheck)((topic*2+2)%6),"door",LayoutFinding.Compliant),"Cross-topic submission must not count");
            Require(attempt.Submit(first,Subject(first,scene.Fault),scene.Expected(first)),"First evidence");
            attempt.Submit(first,Subject(first,scene.Fault),scene.Expected(first));
            Require(!attempt.Complete,"Repeated same check cannot replace second check");
            attempt.Submit(second,Subject(second,scene.Fault),scene.Expected(second));
            Require(attempt.Complete&&attempt.Independent,"Two unassisted facts complete topic");
            attempt.Begin(topic,scene);attempt.Hint();
            attempt.Submit(first,Subject(first,scene.Fault),scene.Expected(first));attempt.Submit(second,Subject(second,scene.Fault),scene.Expected(second));
            Require(attempt.Complete&&!attempt.Independent,"Hinted completion is not independent mastery");
            attempt.Begin(topic,scene);attempt.Submit(first,"wrong-object",scene.Expected(first));
            attempt.Submit(first,Subject(first,scene.Fault),scene.Expected(first));attempt.Submit(second,Subject(second,scene.Fault),scene.Expected(second));
            Require(attempt.Complete&&!attempt.Independent,"Correction is not independent mastery");
            attempt.Begin(topic,scene);Require(!attempt.HadError&&!attempt.Assisted&&!attempt.Complete,"New attempt must reset history");
        }
        Console.WriteLine("PASS: "+assertions+" layout evidence and progression assertions.");
    }
}
