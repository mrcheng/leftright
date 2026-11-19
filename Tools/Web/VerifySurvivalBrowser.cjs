// Real one-lane deaths and named global scores in two isolated browser profiles.
// Writes only QA aliases; CleanSurvivalQa.cs removes those exact player/run pairs.
const fs=require('fs'),path=require('path'),os=require('os'),assert=require('assert/strict');
const {chromium,devices}=require(process.env.PLAYWRIGHT_MODULE||path.join(os.homedir(),'.codex/tmp/leftright-web-qa/node_modules/playwright'));
const url=process.argv[2]||'http://127.0.0.1:8123',out=path.resolve('Logs/WebQA');fs.mkdirSync(out,{recursive:true});
const records=[],errors=[];let activeBrowser;
const recordPath=path.join(out,'survival-test-players.json');
const persist=()=>fs.writeFileSync(recordPath,JSON.stringify({records},null,2));persist();
async function tap(page,x,y){const r=await page.locator('#game').boundingBox(),scale=Math.min(r.width/1280,r.height/720);await page.touchscreen.tap(r.x+(r.width-1280*scale)/2+x*scale,r.y+(r.height-720*scale)/2+y*scale)}
async function run(label,previous){
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||undefined,args:['--enable-unsafe-swiftshader','--use-angle=swiftshader']});activeBrowser=browser;
 const page=await browser.newPage({...devices['Pixel 7'],viewport:{width:844,height:390},deviceScaleFactor:1});
 let submittedId,expectedRun;page.on('pageerror',e=>errors.push(e.message));
 page.on('response',async response=>{
  if(!response.url().includes('survival_all_time')||response.request().method()!=='POST'||!response.ok())return;
  try{const body=await response.json();if(body.playerId){submittedId=body.playerId;const runId=body.metadata?.runId||expectedRun;
   if(runId&&!records.some(record=>record.playerId===submittedId)){records.push({playerId:submittedId,runId,name:label});persist()}}}catch{}
 });
 try{
  await page.goto(url);await page.waitForFunction(()=>window.leftRightLobby?.menu&&window.leftRightResults,null,{timeout:240000});
  await page.screenshot({path:path.join(out,label+'-menu.png')});
  await tap(page,640,562);
  await page.waitForFunction(()=>window.leftRightResults.survival&&window.leftRightLobby.playing&&!window.leftRightLobby.menu);
  await page.screenshot({path:path.join(out,label+'-one-lane.png')});
  await page.waitForFunction(()=>window.leftRightLobby.over,null,{timeout:90000});const death=Date.now();
  await page.waitForFunction(()=>window.leftRightResults.visible,null,{timeout:5000});
  assert(Date.now()-death<4500,'Survival opens results without a winner sequence');
  await page.waitForFunction(()=>window.leftRightResults.hasTable&&!window.leftRightResults.loading,null,{timeout:30000});
  const before=await page.evaluate(()=>window.leftRightResults);expectedRun=before.runId;
  assert(before.survival&&before.qualified&&before.round.length===1&&!before.round[0].winner,'one dead recruit qualifies, without a winner');
  if(previous)assert(before.top10.some(entry=>entry.name===previous),'another browser sees the previous global score');
  for(const id of ['#result-again','#result-menu','#score-name']){const r=await page.locator(id).boundingBox();assert(r.height>=44,id+' is thumb sized')}
  assert(await page.locator('#score-name').evaluate(node=>parseFloat(getComputedStyle(node).fontSize)>=16),'name input avoids iPhone focus zoom');
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'no horizontal page overflow');
  await page.locator('#score-name').fill(label);await page.locator('#score-name').press('Enter');
  await page.waitForFunction(()=>window.leftRightResults.saved,null,{timeout:30000});
  const saved=await page.evaluate(()=>window.leftRightResults);
  assert(saved.visible&&saved.runId===expectedRun,'typing Enter saves instead of restarting the game');
  assert(saved.top10.some(entry=>entry.name===label&&entry.runId===expectedRun),'saved name appears in the real global table');
  assert.equal(saved.personal,before.personal,'saving adds no survival time');
  assert(!saved.qualified,'one score cannot be submitted twice');
  for(let i=0;i<50&&!submittedId;i++)await page.waitForTimeout(100);
  assert(submittedId&&records.some(record=>record.playerId===submittedId),'recorded exact QA player for cleanup');
  const scroll=page.locator('.results-scroll');await scroll.evaluate(node=>node.scrollTop=node.scrollHeight);
  await page.screenshot({path:path.join(out,label+'-global-score.png')});
  if(!previous){
   await page.locator('#result-again').tap();await page.waitForFunction(run=>!window.leftRightResults.visible&&window.leftRightResults.survival&&window.leftRightResults.runId!==run,expectedRun);
   await page.keyboard.press('Escape');await page.waitForFunction(()=>window.leftRightLobby.menu&&!window.leftRightResults.survival);
  }else{
   await page.locator('#result-menu').tap();await page.waitForFunction(()=>window.leftRightLobby.menu&&!window.leftRightResults.visible&&!window.leftRightResults.survival);
  }
  console.log('PASS '+label+': one lane, death/results, named global score and '+(previous?'menu':'replay'));
 }catch(error){await page.screenshot({path:path.join(out,label+'-survival-failure.png')}).catch(()=>{});console.error(await page.evaluate(()=>window.leftRightResults).catch(()=>null));throw error}
 finally{await browser.close();activeBrowser=null}
}
(async()=>{
 const suffix=Date.now().toString(36).slice(-4).toUpperCase(),first='QA_A_'+suffix,second='QA_B_'+suffix;
 await run(first);await run(second,first);assert(!errors.length,errors.join('\n'));
 fs.writeFileSync(path.join(out,'survival-result.json'),JSON.stringify({url,checks:'two real one-lane runs, no winner sequence, global cross-browser names/times, Enter submission, mobile sizing, scroll, replay and menu',errors},null,2));
})().catch(async error=>{persist();if(activeBrowser)await activeBrowser.close().catch(()=>{});console.error(error);process.exitCode=1});
