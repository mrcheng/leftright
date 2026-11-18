// The presenter owns scoring/qualification. This view only renders public result
// data and sends user actions back to Unity, using real mobile inputs and scrolling.
(()=>{
 const panel=document.getElementById('round-results'),name=document.getElementById('score-name'),form=document.getElementById('score-form');
 let state={},run=null,roundDigest='',boardDigest='';
 const text=(id,value)=>{const node=document.getElementById(id);if(node.textContent!==value)node.textContent=value};
 const send=(method,value='')=>browserMessage(method,value);
 function fitViewport(){if(panel.hidden)return;const viewport=window.visualViewport;panel.style.height=(viewport?.height||innerHeight)+'px';panel.style.top=(viewport?.offsetTop||0)+'px'}
 function saveEnabled(){document.getElementById('score-save').disabled=!!state.submitting||!name.value.trim()}
 function focusGame(){send('SetBrowserTextFocus','0');canvas.focus()}
 function rowCell(row,value,tag='td'){const cell=document.createElement(tag);cell.textContent=value;row.appendChild(cell);return cell}
 window.addEventListener('leftright-results',event=>{
  state=event.detail;window.leftRightResults=state;
  const opening=panel.hidden&&state.visible;
  panel.hidden=!state.visible;document.body.classList.toggle('results-open',!!state.visible);
  if(!state.visible){if(panel.contains(document.activeElement)){document.activeElement.blur();focusGame()}return}
  if(opening){resetInput();fitViewport();send('SetBrowserTextFocus','1');document.getElementById('results-title').focus();panel.querySelector('.results-scroll').scrollTop=0}
  if(run!==state.runId){run=state.runId;name.value=state.suggestedName||'';roundDigest=boardDigest=''}
  text('results-title',state.survival?'SURVIVAL RESULTS':'ROUND RESULTS');
  text('result-personal',state.personal);text('result-best',(state.newBest?'NEW PERSONAL BEST  ':'PERSONAL BEST  ')+state.best);
  const round=JSON.stringify(state.round);
  if(round!==roundDigest){
   roundDigest=round;const container=document.getElementById('result-round');container.replaceChildren();
   for(const entry of state.round||[]){const row=document.createElement('div');row.className='round-row'+(entry.winner?' winner':'');
    const label=document.createElement('span');label.textContent=entry.name+(entry.you?' (YOU)':'');
    if(entry.winner){const badge=document.createElement('small');badge.textContent=' WINNER';label.appendChild(badge)}
    const time=document.createElement('time');time.textContent=entry.time;row.append(label,time);container.appendChild(row)}
  }
  document.getElementById('honor-board').hidden=!state.survival;
  form.hidden=!state.survival||!state.qualified;name.disabled=!!state.submitting;
  text('score-status',state.status||'');text('score-save',state.submitting?'...':'SAVE');saveEnabled();
  document.getElementById('score-retry').hidden=!state.survival||state.loading||state.hasTable;
  const digest=JSON.stringify([state.top10,state.loading,state.hasTable,state.runId]);
  if(digest!==boardDigest){
   boardDigest=digest;const rows=document.getElementById('score-rows');rows.replaceChildren();
   if(!state.hasTable||!state.top10?.length){const row=document.createElement('tr');row.className='empty';
    const cell=rowCell(row,state.loading?'Loading the top 10...':state.hasTable?'No scores yet. Set the first record!':'Scores are unavailable.');cell.colSpan=3;rows.appendChild(row)}
   else for(let i=0;i<state.top10.length;i++){const entry=state.top10[i],row=document.createElement('tr');if(entry.runId===state.runId)row.className='current';
    rowCell(row,String(i+1).padStart(2,'0'));rowCell(row,entry.name);const hundredths=entry.hundredths;
    rowCell(row,String(Math.floor(hundredths/6000)).padStart(2,'0')+':'+String(Math.floor(hundredths/100)%60).padStart(2,'0')+'.'+String(hundredths%100).padStart(2,'0'));rows.appendChild(row)}
  }
  document.getElementById('result-again').disabled=!state.canReplay;text('result-again',state.canReplay?'PLAY AGAIN':'WAITING FOR HOST');
 });
 name.addEventListener('input',saveEnabled);
 name.addEventListener('focus',()=>{send('SetBrowserTextFocus','1');setTimeout(()=>{if(!panel.hidden&&document.activeElement===name)name.scrollIntoView({block:'center',behavior:'smooth'})},250)});
 name.addEventListener('blur',()=>send('SetBrowserTextFocus',panel.hidden?'0':'1'));
 form.addEventListener('submit',event=>{event.preventDefault();if(state.submitting||!state.qualified)return;document.getElementById('score-save').disabled=true;send('SubmitSurvivalName',name.value)});
 document.getElementById('score-retry').onclick=()=>send('RetrySurvivalScores');
 document.getElementById('result-again').onclick=()=>{if(!state.canReplay)return;send('ReplayFromResults');focusGame()};
 document.getElementById('result-menu').onclick=()=>{send('MenuFromResults');focusGame()};
 window.addEventListener('resize',fitViewport);window.visualViewport?.addEventListener('resize',fitViewport);window.visualViewport?.addEventListener('scroll',fitViewport);
 panel.addEventListener('keydown',event=>{
  if(event.key==='Escape'){event.preventDefault();send('MenuFromResults');focusGame()}
  if(event.key!=='Tab')return;
  const elements=[...panel.querySelectorAll('input:not(:disabled),button:not(:disabled)')].filter(element=>element.getClientRects().length);
  const first=elements[0],last=elements[elements.length-1];
  if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus()}
  else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus()}
 });
})();
