(()=>{
  if(!document.body.classList.contains('docs-body'))return;

  const style=document.createElement('link');style.rel='stylesheet';style.href='./docs-polish.css';document.head.appendChild(style);

  const pages={
    'docs.html':{title:'Overview',group:'Get started'},
    'concepts.html':{title:'Core concepts',group:'Learn'},
    'querying.html':{title:'Querying',group:'Learn'},
    'guides.html':{title:'Guides & cookbook',group:'Learn'},
    'examples.html':{title:'Runnable examples',group:'Learn'},
    'advanced.html':{title:'Advanced capabilities',group:'Deeper graph'},
    'reference.html':{title:'API reference',group:'Reference'}
  };

  const file=location.pathname.split('/').pop()||'docs.html';
  const page=pages[file]??{title:document.querySelector('h1')?.textContent?.trim()??'Documentation',group:'Docs'};

  function addProgress(){
    const bar=document.createElement('div');bar.className='reading-progress';bar.setAttribute('aria-hidden','true');
    const value=document.createElement('span');bar.appendChild(value);document.body.prepend(bar);
    const update=()=>{const height=document.documentElement.scrollHeight-innerHeight;const ratio=height>0?Math.min(1,scrollY/height):0;value.style.transform=`scaleX(${ratio})`;};
    addEventListener('scroll',update,{passive:true});addEventListener('resize',update,{passive:true});update();
  }

  function addBreadcrumbs(){
    const main=document.querySelector('.docs-content');const hero=main?.querySelector('.docs-hero');if(!main||!hero)return;
    const nav=document.createElement('nav');nav.className='docs-breadcrumbs';nav.setAttribute('aria-label','Breadcrumb');
    const home=document.createElement('a');home.href='./docs.html';home.textContent='Docs';nav.appendChild(home);
    if(page.group!=='Get started'){const separator=document.createElement('span');separator.textContent='/';const group=document.createElement('span');group.textContent=page.group;nav.append(separator,group);}
    const separator=document.createElement('span');separator.textContent='/';const current=document.createElement('strong');current.textContent=page.title;nav.append(separator,current);
    main.insertBefore(nav,hero);
  }

  function addPageTools(){
    const hero=document.querySelector('.docs-content .docs-hero');if(!hero)return;
    const tools=document.createElement('div');tools.className='page-tools';
    const edit=document.createElement('a');edit.href=`https://github.com/pjotrcasteel/GORM/edit/main/docs/${file}`;edit.target='_blank';edit.rel='noreferrer';edit.textContent='✎ Edit this page';
    const source=document.createElement('a');source.href=`https://github.com/pjotrcasteel/GORM/blob/main/docs/${file}`;source.target='_blank';source.rel='noreferrer';source.textContent='↗ View source';
    const copy=document.createElement('button');copy.type='button';copy.textContent='⧉ Copy page link';copy.addEventListener('click',async()=>{try{await navigator.clipboard.writeText(location.href);copy.textContent='✓ Link copied';setTimeout(()=>copy.textContent='⧉ Copy page link',1300);}catch{copy.textContent='Copy URL from address bar';}});
    tools.append(edit,source,copy);hero.appendChild(tools);
  }

  function isGormQuery(text){return text.includes('context.Set<')||text.includes('.Outgoing<')||text.includes('.Incoming<')||text.includes('.AsOf(');}
  function encodeQuery(value){const bytes=new TextEncoder().encode(value);let binary='';bytes.forEach(byte=>binary+=String.fromCharCode(byte));return btoa(binary).replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');}
  function playgroundUrl(query){const url=new URL('./playground.html',location.href);url.searchParams.set('q',encodeQuery(query));return url.href;}

  function addCodeControls(){
    document.querySelectorAll('pre > code').forEach(code=>{
      const pre=code.parentElement;if(!pre||pre.closest('.code-sample'))return;
      const text=code.textContent.trim();
      const existingRunnable=pre.closest('.runnable-code');
      if(existingRunnable){
        const bar=existingRunnable.querySelector('.runnable-code-bar');if(!bar)return;
        const copy=document.createElement('button');copy.type='button';copy.className='code-copy';copy.textContent='Copy';copy.addEventListener('click',()=>copyCode(copy,text));bar.appendChild(copy);return;
      }
      const wrapper=document.createElement('div');wrapper.className='code-sample';pre.parentNode.insertBefore(wrapper,pre);wrapper.appendChild(pre);
      const toolbar=document.createElement('div');toolbar.className='code-toolbar';
      const label=document.createElement('span');label.textContent=isGormQuery(text)?'GORM / C#':'Code';toolbar.appendChild(label);
      if(isGormQuery(text)){const run=document.createElement('a');run.href=playgroundUrl(text);run.textContent='Run in playground';toolbar.appendChild(run);}
      const copy=document.createElement('button');copy.type='button';copy.className='code-copy';copy.textContent='Copy';copy.addEventListener('click',()=>copyCode(copy,text));toolbar.appendChild(copy);
      wrapper.insertBefore(toolbar,pre);
    });
  }

  async function copyCode(button,text){try{await navigator.clipboard.writeText(text);button.textContent='Copied';setTimeout(()=>button.textContent='Copy',1200);}catch{button.textContent='Select code';}}

  function enableSearchShortcut(){
    const input=document.getElementById('docs-search');if(!input)return;
    const hint=document.createElement('kbd');hint.className='search-shortcut';hint.textContent='/';input.parentElement?.appendChild(hint);
    addEventListener('keydown',event=>{if(event.key!=='/'||event.ctrlKey||event.metaKey||event.altKey)return;const active=document.activeElement;if(active&&(active.tagName==='INPUT'||active.tagName==='TEXTAREA'||active.isContentEditable))return;event.preventDefault();input.focus();input.select();});
  }

  function enableActiveToc(){
    const links=[...document.querySelectorAll('.toc a[href^="#"]')];if(!links.length)return;
    const sections=links.map(link=>document.getElementById(link.hash.slice(1))).filter(Boolean);if(!sections.length)return;
    const byId=new Map(links.map(link=>[link.hash.slice(1),link]));
    const setActive=id=>links.forEach(link=>link.classList.toggle('active',link===byId.get(id)));
    const observer=new IntersectionObserver(entries=>{const visible=entries.filter(x=>x.isIntersecting).sort((a,b)=>a.boundingClientRect.top-b.boundingClientRect.top);if(visible[0])setActive(visible[0].target.id);},{rootMargin:'-18% 0px -68% 0px',threshold:[0,.2,1]});
    sections.forEach(section=>observer.observe(section));if(location.hash&&byId.has(location.hash.slice(1)))setActive(location.hash.slice(1));else setActive(sections[0].id);
  }

  function addHeadingLinks(){
    document.querySelectorAll('.doc-section[id] > h2, .doc-section[id] > .section-heading h2').forEach(heading=>{
      const section=heading.closest('.doc-section');if(!section?.id||heading.querySelector('.heading-anchor'))return;
      const link=document.createElement('a');link.className='heading-anchor';link.href=`#${section.id}`;link.setAttribute('aria-label',`Link to ${heading.textContent.trim()}`);link.textContent='#';heading.appendChild(link);
    });
  }

  function addFeedback(){
    const main=document.querySelector('.docs-content');if(!main||main.querySelector('.page-feedback'))return;
    const navigation=main.querySelector('.doc-page-nav');
    const panel=document.createElement('section');panel.className='page-feedback';
    const copy=document.createElement('div');const eyebrow=document.createElement('span');eyebrow.textContent='DOCUMENTATION FEEDBACK';const title=document.createElement('strong');title.textContent='Was this page helpful?';const text=document.createElement('p');text.textContent='GORM documentation lives with the code. Feedback can turn directly into a repository improvement.';copy.append(eyebrow,title,text);
    const actions=document.createElement('div');
    const yes=document.createElement('button');yes.type='button';yes.textContent='Yes';yes.addEventListener('click',()=>{yes.textContent='Thanks ✓';yes.disabled=true;no.disabled=true;});
    const no=document.createElement('button');no.type='button';no.textContent='Needs improvement';no.addEventListener('click',()=>{const issue=new URL('https://github.com/pjotrcasteel/GORM/issues/new');issue.searchParams.set('title',`Docs: improve ${page.title}`);issue.searchParams.set('body',`Page: ${location.href}\n\nWhat was unclear, missing or incorrect?\n\n`);window.open(issue,'_blank','noopener');});
    const edit=document.createElement('a');edit.href=`https://github.com/pjotrcasteel/GORM/edit/main/docs/${file}`;edit.target='_blank';edit.rel='noreferrer';edit.textContent='Edit directly';actions.append(yes,no,edit);panel.append(copy,actions);
    if(navigation)main.insertBefore(panel,navigation);else main.appendChild(panel);
  }

  addProgress();addBreadcrumbs();addPageTools();addCodeControls();enableSearchShortcut();enableActiveToc();addHeadingLinks();addFeedback();
})();