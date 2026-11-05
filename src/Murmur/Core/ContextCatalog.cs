// Derived from OpenWhispr toneAppCategories.js, MIT © 2024 OpenWhispr Team.
// Revision d5821a7c88f4c1803b06c51b4580f16c51dd0dbe. See third_party/openwhispr/LICENSE.
namespace Murmur.Core;
public static partial class AppContextClassifier
{
    private static readonly string[] APP_SIGNATURES_personal = ["whatsapp","telegram","telegramdesktop","keepcoder","messages","mobilesms","phoneexperiencehost","signal","messenger","instagram","facebook","wechat","weixin","xinwechat","naver.line","line.exe","viber","kakaotalk","kakaotalkmac","discord","beeper","threema","zalo"];
    private static readonly string[] APP_SIGNATURES_work = ["slack","teams","msteams","teams-for-linux","google chat","linkedin","mattermost","rocket.chat","rocketchat","zulip","webex","zoom","zoom.us","lark","feishu","dingtalk","dingtalkmac","wxwork","wecom","weworkmac","chanty","flock","pumble","ringcentral"];
    private static readonly string[] APP_SIGNATURES_email = ["mail","outlook","olk","hxoutlook","gmail","superhuman","spark","airmail","thunderbird","betterbird","mimestream","hey","protonmail","canarymail","postbox","em client","mailclient","mailspring","evolution","geary","kmail","kmail2","bluemail","missive","shortwave","tutanota"];
    private static readonly string[] BROWSER_SIGNATURES = ["chrome","chromium","safari","firefox","msedge","microsoft edge","microsoft-edge","arc","brave","opera","vivaldi","zen","orion","dia","comet","librewolf","waterfox","floorp","thorium","yandex","duckduckgo","sidekick"];
    private static readonly string[] SITE_TITLES_personal = ["whatsapp","whatsapp business","telegram","telegram web","instagram","messenger","facebook","x","discord","google messages","messages for web","phone link"];
    private static readonly string[] SITE_TITLES_work = ["slack","microsoft teams","google chat","linkedin","zoom"];
    private static readonly string[] SITE_TITLES_email = ["gmail","outlook","yahoo mail","proton mail","fastmail","superhuman","hey","zoho mail","icloud mail"];
}
