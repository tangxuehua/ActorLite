namespace Crawling
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using ActorLite;

    static class Program
    {
        static void Main(string[] args)
        {
            var monitor = new Monitor(50);
            monitor.Post(m => m.Crawl("http://www.cnblogs.com/"));
            new StatisticReportActor(monitor).Start();
        }
    }

    internal interface ICrawlRequestHandler
    {
        void Crawl(IPort<ICrawlResponseHandler> collector, string url);
    }
    internal interface ICrawlResponseHandler
    {
        void Succeeded(IPort<ICrawlRequestHandler> crawler, string url, string content, List<string> links);
        void Failed(IPort<ICrawlRequestHandler> crawler, string url, Exception ex);
    }
    public interface IStatisticRequestHandelr
    {
        void GetCrawledCount(IPort<IStatisticResponseHandler> requester);
        void GetContent(IPort<IStatisticResponseHandler> requester, string url);
    }
    public interface IStatisticResponseHandler
    {
        void ReplyCrawledCount(int count);
        void ReplyContent(string url, string content);
    }

    public class Crawler : Actor<Crawler>, ICrawlRequestHandler
    {
        #region ICrawlRequestHandler Members

        void ICrawlRequestHandler.Crawl(IPort<ICrawlResponseHandler> collector, string url)
        {
            WebClient client = new WebClient();
            client.DownloadStringCompleted += (sender, e) =>
            {
                if (e.Error == null)
                {
                    var matches = Regex.Matches(e.Result, @"href=""(http://[^""]+)""").Cast<Match>();
                    var links = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
                    collector.Post(c => c.Succeeded(this, url, e.Result, links));
                }
                else
                {
                    collector.Post(c => c.Failed(this, url, e.Error));
                }
            };

            client.DownloadStringAsync(new Uri(url));
        }

        #endregion
    }
    public class Monitor : Actor<Monitor>, ICrawlResponseHandler, IStatisticRequestHandelr
    {
        private HashSet<string> m_allUrls;
        private Queue<string> m_readyToCrawl;
        private Dictionary<string, string> m_urlContent;

        public int MaxCrawlerCount { private set; get; }
        public int WorkingCrawlerCount { private set; get; }

        public Monitor(int crawlerCount)
        {
            this.m_allUrls = new HashSet<string>();
            this.m_readyToCrawl = new Queue<string>();
            this.m_urlContent = new Dictionary<string, string>();
            this.MaxCrawlerCount = crawlerCount;
            this.WorkingCrawlerCount = 0;
        }

        public void Crawl(string url)
        {
            if (this.m_allUrls.Contains(url)) return;
            this.m_allUrls.Add(url);

            if (this.WorkingCrawlerCount < this.MaxCrawlerCount)
            {
                this.WorkingCrawlerCount++;
                IPort<ICrawlRequestHandler> crawler = new Crawler();
                crawler.Post(c => c.Crawl(this, url));
            }
            else
            {
                this.m_readyToCrawl.Enqueue(url);
            }
        }

        #region ICrawlResponseHandler Members

        void ICrawlResponseHandler.Succeeded(IPort<ICrawlRequestHandler> crawler, string url, string content, List<string> links)
        {
            this.m_urlContent[url] = content;
            Console.WriteLine("{0} crawled, {1} link(s).", url, links.Count);

            foreach (var newUrl in links)
            {
                if (!this.m_allUrls.Contains(newUrl))
                {
                    this.m_allUrls.Add(newUrl);
                    this.m_readyToCrawl.Enqueue(newUrl);
                }
            }

            this.DispatchCrawlingTasks(crawler);
        }

        void ICrawlResponseHandler.Failed(IPort<ICrawlRequestHandler> crawler, string url, Exception ex)
        {
            Console.WriteLine("{0} error occurred: {1}.", url, ex.Message);
            this.DispatchCrawlingTasks(crawler);
        }

        #endregion

        #region IStatisticRequestHandelr Members

        void IStatisticRequestHandelr.GetCrawledCount(IPort<IStatisticResponseHandler> requester)
        {
            requester.Post(r => r.ReplyCrawledCount(this.m_urlContent.Count));
        }

        void IStatisticRequestHandelr.GetContent(IPort<IStatisticResponseHandler> requester, string url)
        {
            string content;
            if (!this.m_urlContent.TryGetValue(url, out content))
            {
                content = null;
            }

            requester.Post(r => r.ReplyContent(url, content));
        }

        #endregion

        private void DispatchCrawlingTasks(IPort<ICrawlRequestHandler> reusableCrawler)
        {
            if (this.m_readyToCrawl.Count <= 0)
            {
                this.WorkingCrawlerCount--;
            }

            var url = this.m_readyToCrawl.Dequeue();
            reusableCrawler.Post(c => c.Crawl(this, url));

            while (this.m_readyToCrawl.Count > 0 &&
                this.WorkingCrawlerCount < this.MaxCrawlerCount)
            {
                var newUrl = this.m_readyToCrawl.Dequeue();
                IPort<ICrawlRequestHandler> crawler = new Crawler();
                var a = new Action<ICrawlRequestHandler>(c => c.Crawl(this, newUrl));
                crawler.Post(a);

                this.WorkingCrawlerCount++;
            }
        }
    }
    public class StatisticReportActor : Actor<StatisticReportActor>, IStatisticResponseHandler
    {
        private IPort<IStatisticRequestHandelr> m_statisticPort;

        public StatisticReportActor(IPort<IStatisticRequestHandelr> statisticPort)
        {
            this.m_statisticPort = statisticPort;
        }

        public void Start()
        {
            while (true)
            {
                Console.ReadLine();
                this.m_statisticPort.Post(s => s.GetCrawledCount(this));
                this.m_statisticPort.Post(s => s.GetContent(this, @"http://home.cnblogs.com/q/"));
            }
        }

        void IStatisticResponseHandler.ReplyCrawledCount(int count)
        {
            Console.WriteLine("Crawled: {0}", count);
        }

        void IStatisticResponseHandler.ReplyContent(string url, string content)
        {
            Console.WriteLine("URL: {0}", url);
            Console.WriteLine("content: {0}", content);
        }
    }
}