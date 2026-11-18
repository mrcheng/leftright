// Actual cloud/Relay discovery through the game UI, including a landscape phone.
// Three independent browsers are used while waiting; the observer closes before
// gameplay so software rendering cannot make a live-round input check miss its window.
const fs=require('fs'),path=require('path'),os=require('os'),assert=require('assert/strict');
const {chromium,devices}=require(process.env.PLAYWRIGHT_MODULE||path.join(os.homedir(),'.codex/tmp/leftright-web-qa/node_modules/playwright'));
const url=process.argv[2]||'http://127.0.0.1:8123',out=path.resolve('Logs/WebQA');
fs.mkdirSync(out,{recursive:true});
const browsers=[],pages=[],errors=[];
async function open(label,phone=false){
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||undefined,args:['--enable-unsafe-swiftshader','--use-angle=swiftshader','--disable-background-timer-throttling','--disable-renderer-backgrounding']});
 browsers.push(browser);const page=await browser.newPage(phone?{...devices['Pixel 7'],viewport:{width:844,height:390},deviceScaleFactor:1}:{viewport:{width:1280,height:900}});
 pages.push({page,label});page.on('pageerror',e=>errors.push(label+': '+e.message));
 await page.goto(url);await page.waitForFunction(()=>window.leftRightLobby?.menu&&window.leftRightDrills,null,{timeout:240000});
 return page;
}
async function tap(page,x,y,portrait=false){
 const r=await page.locator('#game').boundingBox(),w=1280,h=720,scale=Math.min(r.width/w,r.height/h);
 const point={x:r.x+(r.width-w*scale)/2+x*scale,y:r.y+(r.height-h*scale)/2+y*scale};
 if(portrait)await page.touchscreen.tap(point.x,point.y);else await page.mouse.click(point.x,point.y);
 await page.waitForTimeout(250);
}
const timeout={timeout:45000};
(async()=>{
 try{
  const host=await open('public-host');assert(await host.evaluate(()=>window.leftRightDrills.isPublic),'new hosts default to public');
  await tap(host,640,430);await tap(host,1050,452);
  await host.waitForFunction(()=>!window.leftRightDrills.isPublic);
  await tap(host,640,457);await host.waitForFunction(()=>window.leftRightLobby.code.length===6,null,timeout);
  const code=await host.evaluate(()=>window.leftRightLobby.code);
  assert(!await host.evaluate(()=>window.leftRightDrills.listed),'private host not listed');
  const peer=await open('public-peer',true);
  await peer.waitForFunction(()=>!window.leftRightDrills.refreshing&&!window.leftRightDrills.status.includes('Looking'),null,timeout);
  assert(!await peer.evaluate(code=>window.leftRightDrills.rooms.some(r=>r.JoinCode===code),code),'private room absent from another phone');
  assert(!await peer.evaluate(()=>window.leftRightDrills.status.includes('Could not')),'cloud queries available');
  console.log('Private room stays out of the public list');
  await tap(host,1050,452);await host.waitForFunction(()=>window.leftRightDrills.listed,null,timeout);
  await peer.waitForFunction(code=>window.leftRightDrills.rooms.some(r=>r.JoinCode===code&&r.Players===1&&r.Capacity===4),code,timeout);
  assert.equal(await peer.evaluate(code=>window.leftRightDrills.rooms.filter(r=>r.JoinCode===code).length,code),1,'one listing per host');
  await peer.screenshot({path:path.join(out,'public-landscape-list.png')});
  await tap(host,1050,452);
  await peer.waitForFunction(code=>!window.leftRightDrills.rooms.some(r=>r.JoinCode===code),code,timeout);
  console.log('Checkbox removes a live public room');
  await tap(host,1050,452);
  await peer.waitForFunction(code=>window.leftRightDrills.rooms.some(r=>r.JoinCode===code),code,timeout);
  const index=await peer.evaluate(code=>window.leftRightDrills.rooms.findIndex(r=>r.JoinCode===code),code);
  for(let page=0;page<Math.floor(index/4);page++)await tap(peer,1228,602,true);
  await tap(peer,1212,376+(index%4)*57,true);
  await host.waitForFunction(()=>window.leftRightLobby.humans===2,null,timeout);
  await peer.waitForFunction(()=>window.leftRightLobby.humans===2,null,timeout);
  console.log('Phone joined the Relay room directly from the first screen');
  const observer=await open('public-observer');
  await observer.waitForFunction(code=>window.leftRightDrills.rooms.some(r=>r.JoinCode===code&&r.Players===2&&r.Capacity===4),code,timeout);
  await observer.screenshot({path:path.join(out,'public-two-of-four.png')});
  await observer.context().browser().close();
  await tap(host,640,500);await peer.waitForFunction(()=>window.leftRightLobby.playing,null,timeout);
  await host.waitForFunction(()=>!window.leftRightDrills.listed,null,timeout);
  await peer.waitForTimeout(3300);
  const start=await peer.evaluate(()=>window.leftRightLobby.x),move=await peer.locator('#move-right').boundingBox(),jump=await peer.locator('#jump-right').boundingBox();
  const cdp=await peer.context().newCDPSession(peer);
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{id:1,x:move.x+move.width/2,y:move.y+move.height/2},{id:2,x:jump.x+jump.width/2,y:jump.y+jump.height/2}]});
  await peer.waitForFunction(x=>window.leftRightLobby.x>x+.1&&window.leftRightLobby.jumping,start,{timeout:8000});
  await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  await peer.screenshot({path:path.join(out,'public-joined-playing.png')});
  assert(!errors.length,errors.join('\n'));
  fs.writeFileSync(path.join(out,'public-result.json'),JSON.stringify({url,checks:'public default, private exclusion, visibility toggle, landscape first-screen join, actual 2/4 occupancy on another main menu, match start removes listing, touch move+jump',errors},null,2));
  console.log('PASS public discovery, visibility, live counts, joining, start and touch gameplay');
 }catch(error){
  for(const {page,label} of pages)await page.screenshot({path:path.join(out,label+'-failure.png')}).catch(()=>{});
  fs.writeFileSync(path.join(out,'public-errors.json'),JSON.stringify({error:String(error),errors},null,2));throw error;
 }finally{for(const browser of browsers)await browser.close().catch(()=>{})}
})().catch(e=>{console.error(e);process.exitCode=1});
