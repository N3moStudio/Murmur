(() => {
 const root=document.querySelector('dialog[open]')||document;
 const rows=[...root.querySelectorAll('.model-download-row')];
 const violations=[];
 for(const row of rows){
  const text=row.querySelector('.model-details').getBoundingClientRect(),button=row.querySelector('button,.model-ready').getBoundingClientRect(),progress=row.querySelector('.model-progress');
  if(row.scrollWidth>row.clientWidth+1)violations.push('row overflow');
  if(text.right>button.left+1)violations.push('text/action overlap');
  if(button.width<43)violations.push('small download target');
  if(!progress.hidden&&progress.getBoundingClientRect().top<Math.max(text.bottom,button.bottom)-1)violations.push('progress overlaps details');
 }
 const last=rows.at(-1)?.getBoundingClientRect(),footer=root.querySelector('.model-download-footer')?.getBoundingClientRect();
 if(last&&footer&&footer.top<last.bottom-1)violations.push('download all overlaps final row');
 return {width:innerWidth,zoom:document.body.style.zoom||1,theme:document.documentElement.dataset.theme,rows:rows.length,violations};
})()