using System.Collections.Generic;
using System.Text.Json.Serialization;

// JSON deserializer object for Dev Tools /json/list response, which will look like this...
/* 08:42:09.569 INF >> Recorder browser: json / list:[ {
   "description": "",
   "devtoolsFrontendUrl": "/devtools/inspector.html?ws=localhost:9222/devtools/page/C749524254ECC5A4B873B7F08EA830EE",
   "faviconUrl": "https://assets.msn.com/statics/icons/favicon_newtabpage.png",
   "id": "C749524254ECC5A4B873B7F08EA830EE",
   "title": "New tab",
     "type": "page",
     "url": "edge://newtab/",
     "webSocketDebuggerUrl": "ws://localhost:9222/devtools/page/C749524254ECC5A4B873B7F08EA830EE"
}, {
	"description": "",
   "devtoolsFrontendUrl": "/devtools/inspector.html?ws=localhost:9222/devtools/page/FD510D0483A54809021BC8A4E053729F",
   "id": "FD510D0483A54809021BC8A4E053729F",
   "title": "Service Worker https://ntp.msn.com/edge/ntp/service-worker.js?bundles=feat-bundleexp1&amp;raceEnabled=true&amp;riverAgeMinutes=180&amp;navAgeMinutes=2880&amp;enableNavPreload=true",
   "type": "service_worker",
   "url": "https://ntp.msn.com/edge/ntp/service-worker.js?bundles=feat-bundleexp1&raceEnabled=true&riverAgeMinutes=180&navAgeMinutes=2880&enableNavPreload=true",
   "webSocketDebuggerUrl": "ws://localhost:9222/devtools/page/FD510D0483A54809021BC8A4E053729F"
} ] */

namespace BizDeck
{
	public class DevToolsJsonListResponse
	{
		public DevToolsJsonListResponse()
		{
		}
		[JsonPropertyName("description")]
		public string Description { get; set; }

		[JsonPropertyName("devtoolsFrontendUrl")]
		public string DevToolsFrontEndUrl { get; set; }

		[JsonPropertyName("faviconUrl")]
		public string FavIconUrl { get; set; }

		[JsonPropertyName("id")]
		public string Id { get; set; }

		[JsonPropertyName("title")]
		public string Title { get; set; }

		[JsonPropertyName("type")]
		public string Type { get; set; }

		[JsonPropertyName("url")]
		public string Url { get; set; }

		[JsonPropertyName("webSocketDebuggerUrl")]
		public string WebSocketDebuggerUrl { get; set; }
	}
}
