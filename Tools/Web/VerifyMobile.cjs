// Mobile browser regression checks. These emulate phones; they do not replace a
// physical iPhone check for Safari chrome, safe areas and device performance.
const fs=require('fs'),path=require('path'),os=require('os'),assert=require('assert/strict');
const {chromium,webkit,devices}=require(process.env.PLAYWRIGHT_MODULE||path.join(os.homedir(),'.codex/tmp/leftright-web-qa/node_modules/playwright'));
const url=process.argv[2]||'http://127.0.0.1:8123';
const out=path.resolve('Logs/WebQA');fs.mkdirSync(out,{recursive:true});
const launch={headless:true,args:['--enable-unsafe-swiftshader','--use-angle=swiftshader']};
const reports=[];
async function tapGame(page,x,y){const r=await page.locator('#game').boundingBox();const scale=Math.min(r.width/1280,r.height/720);await page.touchscreen.tap(r.x+(r.width-1280*scale)/2+x*scale,r.y+(r.height-720*scale)/2+y*scale);await page.waitForTimeout(300)}
async function check(engine,device,label){
 const browser=await engine.launch(engine===chromium?{...launch,executablePath:process.env.CHROME_PATH||undefined}:{headless:true});
 const page=await browser.newPage({...devices[device],viewport:{width:390,height:844},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 try{
  // Exercise the iPhone fallback even on Chromium. No autoplay bypass: all audio
  // unlocks must originate from the same gestures a phone player would make.
  await page.addInitScript(()=>{Element.prototype.requestFullscreen=undefined;window.__audio=[];const A=window.AudioContext||window.webkitAudioContext;if(A)window.AudioContext=class extends A{constructor(...args){super(...args);window.__audio.push(this)}}});
  await page.goto(url);
  await page.waitForFunction(()=>document.getElementById('loading').style.display==='none',null,{timeout:240000});
  await page.waitForFunction(()=>window.leftRightLobby?.menu,null,{timeout:15000});
  assert(await page.locator('#fullscreen').isHidden(),'no native fullscreen dependency');
  let r=await page.locator('#game').boundingBox();assert(Math.abs(r.width-390)<2&&Math.abs(r.height-844)<2,'portrait fallback fills viewport');
  await page.setViewportSize({width:844,height:390});await page.waitForTimeout(500);
  r=await page.locator('#game').boundingBox();assert(Math.abs(r.width-844)<2&&Math.abs(r.height-390)<2,'landscape fallback fills viewport');
  if(engine===webkit){
   // Windows WebKit has no Web Audio implementation. Check its iPhone-sized
   // viewport/menu/native keyboard layout; actual audio/gameplay is tested in
   // Chromium below and still needs a physical iPhone Safari check.
   await tapGame(page,640,430);await page.waitForFunction(()=>window.leftRightLobby.menuPage===3);
   assert(await page.locator('#join-code').isVisible(),'native mobile room-code field');
   await page.locator('#join-code').fill('ABC123');assert.equal(await page.locator('#join-code').inputValue(),'ABC123');
   await page.screenshot({path:path.join(out,label+'-mobile.png')});
   assert(!errors.length,errors.join('\n'));reports.push({label,checks:'full viewport without native fullscreen, portrait/landscape, menu and native room input',limitation:'Windows WebKit has no Web Audio; no iPhone audio/gameplay claim'});
   console.log('PASS '+label+': full viewport and mobile lobby input (audio unavailable in this test engine)');return;
  }
  await tapGame(page,640,343);await tapGame(page,640,546);
  await page.waitForFunction(()=>window.leftRightLobby?.playing&&!window.leftRightLobby.menu);
  assert(await page.locator('#jump').isVisible(),'touch controls visible');
  const x=await page.evaluate(()=>window.leftRightLobby.x);
  const cdp=await page.context().newCDPSession(page);
  const left={x:r.x+r.width*.25,y:r.y+r.height*.55},right={x:r.x+r.width*.75,y:left.y};
  const jumpBox=await page.locator('#jump').boundingBox(),jump={x:jumpBox.x+jumpBox.width/2,y:jumpBox.y+jumpBox.height/2};
  async function fingers(type,points){
   await cdp.send('Input.dispatchTouchEvent',{type,touchPoints:points.map((p,i)=>({...p,id:i+1}))});
  }
  await fingers('touchStart',[left]);
  await page.waitForFunction(x=>window.leftRightLobby.x<x-.15,x,{timeout:4000});
  await fingers('touchMove',[right]);
  const leftX=await page.evaluate(()=>window.leftRightLobby.x);
  await fingers('touchStart',[right,jump]);
  await page.waitForFunction(x=>window.leftRightLobby.x>x+.1&&window.leftRightLobby.jumping,leftX,{timeout:4000});
  await fingers('touchEnd',[]);
  assert.equal(await page.evaluate(()=>window.leftRightInput.axis),0,'release stops movement');
  await page.waitForFunction(()=>window.leftRightLobby.beat>4&&window.leftRightLobby.travel>.5,null,{timeout:20000});
  const state=await page.evaluate(()=>({...window.leftRightLobby,audio:window.__audio.map(a=>a.state)}));
  assert(state.audio.includes('running'),'audio unlocked by gestures');
  await page.screenshot({path:path.join(out,label+'-mobile.png')});
  await page.setViewportSize({width:390,height:844});await page.waitForTimeout(250);r=await page.locator('#game').boundingBox();assert(Math.abs(r.height-844)<2,'still fills viewport after rotation');
  assert(!errors.length,errors.join('\n'));reports.push({label,state,checks:'portrait/landscape fullscreen fallback, movement in count-in, left/right drag, move+jump, release, audible beat, moving spikes, rotation',errors});
  console.log('PASS '+label+': mobile controls, fullscreen fallback and advancing spikes');
 }catch(error){await page.screenshot({path:path.join(out,label+'-mobile-failure.png')}).catch(()=>{});console.error(label,await page.evaluate(()=>window.leftRightLobby).catch(()=>null),errors);throw error}
 finally{await browser.close()}
}
(async()=>{await check(chromium,'Pixel 7','android');await check(webkit,'iPhone 13','ios-webkit');fs.writeFileSync(path.join(out,'mobile-result.json'),JSON.stringify({url,reports},null,2))})().catch(e=>{console.error(e);process.exitCode=1});
