// UI-only multiplayer smoke test. Two independent browsers stay visible to their
// own game loops, so an inactive tab cannot suspend the host's animation frames.
const fs=require('fs'),path=require('path'),os=require('os');
const {chromium,devices}=require(process.env.PLAYWRIGHT_MODULE||path.join(os.homedir(),'.codex/tmp/leftright-web-qa/node_modules/playwright'));
const url=process.argv[2]||'http://127.0.0.1:8123';
const mobilePeer=process.env.MOBILE_PEER==='1',portraitPeer=mobilePeer&&process.env.MOBILE_PORTRAIT==='1';
const out=path.resolve('Logs/WebQA');fs.mkdirSync(out,{recursive:true});
const errors=[],browsers=[],pages=[];
const launch={headless:true,executablePath:process.env.CHROME_PATH||undefined,args:['--enable-unsafe-swiftshader','--use-angle=swiftshader','--disable-background-timer-throttling','--disable-renderer-backgrounding']};
async function open(label){
 const browser=await chromium.launch(launch);browsers.push(browser);
 const page=await browser.newPage(label==='peer'&&mobilePeer?{...devices['Pixel 7'],viewport:portraitPeer?{width:390,height:844}:{width:844,height:390},deviceScaleFactor:1}:{viewport:{width:1280,height:900}});
 pages.push({page,label});
 page.on('pageerror',error=>errors.push(label+': '+error.message));
 page.on('console',message=>{if(message.type()==='error'&&!message.text().includes('status of 404'))errors.push(label+': '+message.text())});
 await page.goto(url);
 await page.waitForFunction(()=>document.getElementById('loading').style.display==='none',{},{timeout:240000});
 await page.waitForTimeout(1500);await page.screenshot({path:path.join(out,label+'-menu.png')});
 return page;
}
async function click(page,x,y){const r=await page.locator('#game').boundingBox();const scale=Math.min(r.width/1280,r.height/720);await page.mouse.click(r.x+(r.width-1280*scale)/2+x*scale,r.y+(r.height-720*scale)/2+y*scale);await page.waitForTimeout(350)}
(async()=>{
 try{
  const host=await open('host');console.log('Browser host loaded');
  await click(host,640,430);
  if(process.env.PRIVATE_HOST==='1'){
   await click(host,1050,452);await host.waitForFunction(()=>window.leftRightDrills?.isPublic===false);
  }
  await click(host,640,457);
  await host.waitForFunction(()=>window.leftRightLobby?.code?.length===6,{},{timeout:60000});
  const code=await host.evaluate(()=>window.leftRightLobby.code);
  console.log('Browser created Relay room');await host.screenshot({path:path.join(out,'host-room.png')});
  const peer=await open('peer');
  if(portraitPeer){await peer.waitForSelector('#rotate-phone:visible');await peer.setViewportSize({width:844,height:390});await peer.waitForSelector('#rotate-phone',{state:'hidden'})}
  await click(peer,640,430);
  if(mobilePeer){await peer.locator('#join-code').fill(code);await peer.locator('#join-form button').tap()}
  else {await click(peer,550,522);await peer.keyboard.type(code,{delay:120});await click(peer,790,522)}
  await host.waitForFunction(()=>window.leftRightLobby?.humans===2,{},{timeout:60000});
  await peer.waitForFunction(()=>window.leftRightLobby?.humans===2,{},{timeout:60000});
  console.log('Two browser players connected via Relay');await host.screenshot({path:path.join(out,'two-player-lobby.png')});
  await click(host,640,500);
  await peer.waitForFunction(()=>window.leftRightLobby?.playing,{},{timeout:15000});
  await host.waitForTimeout(3300);
  const before=await peer.evaluate(()=>window.leftRightLobby.x);
  let touch;
  if(mobilePeer){
   touch=await peer.context().newCDPSession(peer);const r=await peer.locator('#move-right').boundingBox(),j=await peer.locator('#jump-right').boundingBox();
   await touch.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:r.x+r.width/2,y:r.y+r.height/2,id:1},{x:j.x+j.width/2,y:j.y+j.height/2,id:2}]});
  }else {await peer.keyboard.down('d');await peer.keyboard.press('Space')}
  await peer.waitForFunction(x=>window.leftRightLobby?.x>x+.1&&window.leftRightLobby?.jumping,before,{timeout:5000});
  await peer.screenshot({path:path.join(out,'peer-playing.png')});if(touch)await touch.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});else await peer.keyboard.up('d');
  // Stay in the real match beyond the first obstacle opportunity. A lobby-only
  // connection test would miss stripped runtime types and later main-loop crashes.
  await host.waitForTimeout(9000);
  await host.waitForFunction(()=>window.leftRightLobby?.updatedAt>performance.now()-2000,{},{timeout:10000});
  await peer.waitForFunction(()=>window.leftRightLobby?.updatedAt>performance.now()-2000,{},{timeout:10000});
  await host.screenshot({path:path.join(out,'host-playing.png')});
  const states={host:await host.evaluate(()=>window.leftRightLobby),peer:await peer.evaluate(()=>window.leftRightLobby)};
  const report={url,mobilePeer,states,errors};fs.writeFileSync(path.join(out,'result.json'),JSON.stringify(report,null,2));
  if(errors.some(e=>/Exception|abort|failed to|RuntimeError/i.test(e)))throw Error('Browser runtime errors; see Logs/WebQA/result.json');
  console.log('PASS: browser loading, WSS room hosting/joining, two-player lobby, match start and '+(mobilePeer?'mobile touch input':'keyboard input')+'');
 }catch(error){
  fs.writeFileSync(path.join(out,'errors.json'),JSON.stringify({error:String(error),errors},null,2));
  for(const {page,label} of pages)await page.screenshot({path:path.join(out,label+'-failure.png')}).catch(()=>{});
  throw error;
 }
 finally{for(const browser of browsers)await browser.close()}
})().catch(error=>{console.error(error);process.exitCode=1});
