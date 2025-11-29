open System
open System.IO
open System.Net
open System.Net.Http
open System.Text
open System.Text.RegularExpressions
open System.Threading
open System.Xml
open System.Xml.Linq
type Post =
    {
        Author: string
        Content: string
        Link: string
        Published: DateTime
        Title: string
    }
if not (File.Exists "webhook.txt") then

    Console.WriteLine ("webhook not found in " + Directory.GetCurrentDirectory())
    exit -1
let whurl =  new Uri (File.ReadAllText "webhook.txt")

while true do
    if not (File.Exists "timestamp.txt") then
            let now = DateTime.Now.AddDays -100
            File.WriteAllText ("timestamp.txt", now.ToFileTimeUtc().ToString())
    let lastRun =
        DateTime.FromFileTimeUtc (int64 <| File.ReadAllText("timestamp.txt"))
    let newSubPosts =
        let client = new HttpClient()

        client.BaseAddress <- new Uri("""http://reddit.com/r/tradecraftGame/new.rss""")
        let request = new HttpRequestMessage()
        request.Method <- HttpMethod.Get
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 11; SAMSUNG SM-G973U) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/14.2 Chrome/87.0.4280.141 Mobile Safari/537.36")
        let rssfeed = client.Send(request)

        let reader =
            rssfeed.Content.ReadAsStream()
            |> XmlReader.Create


        seq {
            let root = (XDocument.Load reader).Root
            let xmlns = root.Name.Namespace
            let n (entry: XElement) str =
                entry.Element (xmlns + str)
            let entries = root.Elements () |> Seq.where (fun x -> x.Name.LocalName = "entry") |> List.ofSeq
            for entry in entries do
                let author = (n (n entry "author") "name").Value
                let content = (n entry "content").Value |> fun x -> Regex.Replace (x, "<.*?>|•|\u0026", "")
                let link = ((n entry "link").Attribute "href").Value
                let published = DateTime.Parse ((n entry "published").Value)
                let title = (n entry "title").Value
                if (published > lastRun) then
                    yield {Author =  author; Content = content; Link = link; Published =  published; Title = title}
        }
        |> List.ofSeq
        |> List.rev
    let newDevPosts =
        let client = new HttpClient()

        client.BaseAddress <- new Uri("""https://reddit.com/user/Professional_Low_757/comments.rss""")
        let request = new HttpRequestMessage()
        request.Method <- HttpMethod.Get
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Linux; Android 11; SAMSUNG SM-G973U) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/14.2 Chrome/87.0.4280.141 Mobile Safari/537.36")
        let rssfeed = client.Send(request)

        let reader =
            rssfeed.Content.ReadAsStream()
            |> XmlReader.Create


        seq {
            let root = (XDocument.Load reader).Root
            let xmlns = root.Name.Namespace
            let n (entry: XElement) str =
                entry.Element (xmlns + str)
            let entries = root.Elements () |> Seq.where (fun x -> x.Name.LocalName = "entry") |> List.ofSeq
            for entry in entries do
                let author = (n (n entry "author") "name").Value
                let content = (n entry "content").Value |> fun x -> Regex.Replace (x, "<.*?>|•|\u0026", "")
                let link = ((n entry "link").Attribute "href").Value
                let published = DateTime.Parse ((n entry "updated").Value)
                let title = (n entry "title").Value
                if (published > lastRun) then
                    yield {Author =  author; Content = content; Link = link; Published =  published; Title = title}
        }
        |> List.ofSeq
        |> List.rev
    let newposts =
        List.append newSubPosts newDevPosts
        |> List.sortBy (_.Published)

    let dcclient = new WebClient ()

    for newest in newposts do
        dcclient.Headers.Add ("Content-Type", "application/json")
        let payload = $$"""{
        "embeds": [{
            "color": 16729344,
            "author": {"name": "{{newest.Author}}",  "url": "https://www.reddit.com{{newest.Author}}"},
            "title": "{{newest.Title}}",
            "url": "{{newest.Link}}",
            "description": "{{newest.Content.Substring(0, Math.Min (1500, newest.Content.Length))}}",
            "footer": {"text": "r/tradecraftgame - Posted at {{newest.Published}}"}
            }]

        }"""
        try
            dcclient.UploadData(whurl, Encoding.UTF8.GetBytes payload) |> ignore
        with
        | :? WebException as  we ->
            let rs = new StreamReader (we.Response.GetResponseStream())
            let res = rs.ReadToEnd()
            Console.WriteLine res

        Thread.Sleep(1000*5)
    File.WriteAllText ("timestamp.txt", DateTime.Now.ToFileTimeUtc().ToString())
    Thread.Sleep(1000*60*5)