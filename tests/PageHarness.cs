using System;using System.IO;using System.Threading;using LocalDownload;
class PageHarness {
    static int Main(string[] args){
        var request=Core.Json().Deserialize<Request>(File.ReadAllText(args[0]));Core.Validate(request);
        var job=new Job {Id=Guid.NewGuid().ToString("N"),Url=request.url,Kind=request.kind,Cookies=request.cookies,Path=args[1],Name=Path.GetFileName(args[1]),Cancellation=new CancellationTokenSource()};
        if(args.Length>2)job.Cancellation.CancelAfter(Int32.Parse(args[2]));
        VideoPage.Download(job).GetAwaiter().GetResult();File.WriteAllText(args[0]+".result.json",Core.Json().Serialize(job));return job.State=="Complete"?0:job.State=="Stopped"?2:1;
    }
}
