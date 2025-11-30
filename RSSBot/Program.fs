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
if not (File.Exists "timestamp.txt") then
    File.WriteAllText ("timestamp.txt", DateTime.Now.ToFileTimeUtc().ToString())

let cleanup =
    let rep (p:string) (r:string) (str: string) =
        Regex.Replace(str, p, r)
    rep "\u0026#32;" " "
    >> rep "\u0026#39;" "'"
    >> rep "•" "> "
    >> rep "<a.*?>.*?<.*?>" ""
    >> rep "\"" "\\\""
    >> rep "<!-- SC_OFF -->" ""
    >> rep "<div.*?>" ""
    >> rep "<p>" "\\n"
    >> rep "</p>" ""
    >> rep "<br/>" "\\n"
    >> rep "</div><!-- SC_ON -->" ""
    >> rep "submitted by" ""
    >> rep "<span></span>" ""
    >> rep "</?strong>" "**"
    >> rep "</?em>" "*"
    >> rep "&quot;" "\\\""
    >> rep "<h1>" "\\n# "
    >> rep "<h2>" "\\n## "
    >> rep "<h3>" "\\n### "
    >> rep "<h4>" "\\n#### "
    >> rep "<h5>" "\\n##### "
    >> rep "<h6>" "\\n###### "
    >> rep "<li>" "> "
    >> rep "</li>" "\\n"
    >> rep "</h\d>" "\\n"
    >> rep "</?ol>" "\\n"
    >> _.Trim()
while true do
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
                let content = (n entry "content").Value |> cleanup
                let link = ((n entry "link").Attribute "href").Value
                let published = (DateTime.Parse ((n entry "published").Value)).ToUniversalTime()
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
                let content = (n entry "content").Value |> cleanup
                let link = Regex.Replace (((n entry "link").Attribute "href").Value,"/$","" )+ "?context=3"
                let published = (DateTime.Parse ((n entry "updated").Value)).ToUniversalTime()
                let title = (n entry "title").Value
                if (published > lastRun) then
                    yield {Author =  author; Content = content; Link = link; Published =  published; Title = title}
        }
        |> List.ofSeq
        |> List.rev
    let exectime = DateTime.Now
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
            dcclient.UploadData(whurl, Encoding.UTF8.GetBytes payload) |> ignore


        Thread.Sleep(1000*5)
    File.WriteAllText ("timestamp.txt", exectime.ToFileTimeUtc().ToString())
    Thread.Sleep(1000*60*5)