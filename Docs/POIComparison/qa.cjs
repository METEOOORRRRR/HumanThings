const {chromium} = require('C:/Users/yubin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL} = require('node:url');
const path = require('node:path');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(__dirname,'index.html')).href);
  for(const seed of [100,200,300,400,500]){
   await page.getByRole('tab',{name:'Seed '+seed,exact:true}).click();
   if(await page.locator('#rows tr').count()!==15)throw Error('Missing rows '+seed);
   if(await page.locator('#facades img').count()!==15)throw Error('Missing facades '+seed);
   const loaded=await page.locator('#facades img').evaluateAll(async images=>{
    images.forEach(i=>i.loading='eager');
    return await Promise.all(images.map(async i=>{await i.decode();return i.naturalWidth===960;}));
   });
   if(loaded.some(v=>!v))throw Error('Broken capture '+seed);
  }
  await page.selectOption('#left','100');await page.selectOption('#right','500');
  await page.getByRole('tab',{name:'Seed 100',exact:true}).click();
  await page.screenshot({path:path.join(__dirname,'Gallery-Desktop.png')});
  await page.setViewportSize({width:390,height:844});
  await page.screenshot({path:path.join(__dirname,'Gallery-Mobile.png')});
  if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Page overflow');
  if(errors.length)throw Error(errors.join('\n'));
  console.log('GALLERY_QA_PASS seeds=5 facadeImages=75 desktop=1440 mobile=390 errors=0');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1});
