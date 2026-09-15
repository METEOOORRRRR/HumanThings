const {chromium}=require('C:/Users/yubin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const {pathToFileURL}=require('node:url');
const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const errors=[];
 try{
  const page=await browser.newPage({viewport:{width:1440,height:1000}});
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto(pathToFileURL(path.join(__dirname,'index.html')).href);
  for(const view of ['Street','Boulevard','Detail','Overview']){
   await page.selectOption('#view',view);
   await page.locator('img').evaluateAll(async images=>{for(const image of images){await image.decode();if(image.naturalWidth!==1600)throw Error('Image load failed');}});
  }
  await page.selectOption('#view','Boulevard');
  await page.screenshot({path:path.join(__dirname,'Gallery-Desktop.png'),fullPage:true});
  await page.setViewportSize({width:390,height:844});
  if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))throw Error('Mobile overflow');
  await page.screenshot({path:path.join(__dirname,'Gallery-Mobile.png'),fullPage:true});
  if(errors.length)throw Error(errors.join('\n'));
  console.log('VISUAL_GALLERY_PASS views=4 images=28 desktop=1440 mobile=390 errors=0');
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
