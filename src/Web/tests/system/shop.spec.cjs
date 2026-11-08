const {test,expect}=require('@playwright/test');
const {execFileSync}=require('node:child_process');
const path=require('node:path');
const root=path.resolve(__dirname,'../../../..');
const productId='11111111-1111-1111-1111-111111111111';
function local(...args){execFileSync('powershell.exe',['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(root,'scripts/control-local.ps1'),'-Action',args[0],'-Services',args.slice(1).join(',')],{cwd:root,stdio:'pipe',timeout:240000});}
test.beforeAll(()=>{if(!process.env.LOCAL_CONTROL_DIRECTORY)throw new Error('Run scripts/verify.ps1 -System to start a controlled local system');});
async function stock(request){const r=await request.get((process.env.INVENTORY_URL||'http://127.0.0.1:5102')+'/stock/'+productId);expect(r.ok()).toBeTruthy();return r.json();}
async function create(request,key,quantity=1){const r=await request.post('/api/orders',{headers:{'Idempotency-Key':key},data:{productId,quantity}});expect(r.status()).toBe(202);return r.json();}
async function confirmed(request,id){await expect.poll(async()=>{try {const r=await request.get('/api/orders/'+id);return r.ok() ? (await r.json()).status : 'unavailable';} catch {return 'unavailable';}},{timeout:60000}).toBe('Confirmed');}
async function ready(request){
 for(const url of ['/health/ready',(process.env.INVENTORY_URL||'http://127.0.0.1:5102')+'/health/ready'])
  await expect.poll(async()=>{try{return (await request.get(url)).ok();}catch{return false;}},{timeout:90000,message:'System readiness: '+url}).toBe(true);
}
test.beforeEach(async({request})=>{await ready(request);});
test('gerçek arayüz: onay, stok düşümü ve yetersiz stok',async({page,request},info)=>{
 const before=await stock(request);expect(before.available).toBeGreaterThan(0);
 await page.goto('/');await page.getByLabel('Ürün ara',{exact:true}).fill('Klavye');await page.getByLabel('Para birimi',{exact:true}).selectOption('TRY');await page.getByLabel('Sıralama',{exact:true}).selectOption('price');await expect(page.getByText('1 ürün bulundu',{exact:true})).toBeVisible();
 // Select by product name; catalog ordering is intentionally independent of test order.
 const card=page.locator('article').filter({has:page.getByText('Klavye',{exact:true})});
 await card.getByRole('button',{name:'Ürünü incele'}).click();
 await page.getByRole('button',{name:'Sepete ekle'}).click();await page.getByRole('button',{name:'Sepet (1)',exact:true}).click();
 await page.getByRole('button',{name:'Sipariş ver',exact:true}).click();
 await expect(page.getByText('Sipariş onaylandı.',{exact:true})).toBeVisible({timeout:60000});
 expect((await stock(request)).quantity).toBe(before.quantity-1);
 await page.screenshot({path:info.outputPath('confirmed-desktop.png'),fullPage:true});
 await page.getByRole('button',{name:'Yeni alışveriş'}).click();
 await card.getByRole('button',{name:'Ürünü incele'}).click();await page.getByRole('button',{name:'Sepete ekle'}).click();await page.getByRole('button',{name:'Sepet (1)',exact:true}).click();
 await page.getByLabel('Adet').fill(String(before.quantity+1));await page.getByRole('button',{name:'Sipariş ver',exact:true}).click();
 await expect(page.getByText('Stok yetersiz veya rezervasyon reddedildi. Sipariş oluşturulamadı.',{exact:true})).toBeVisible({timeout:60000});
 expect((await stock(request)).quantity).toBe(before.quantity-1);
 await page.setViewportSize({width:390,height:844});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBeTruthy();
 await page.screenshot({path:info.outputPath('rejected-mobile.png'),fullPage:true});
});
for(const outage of ['inventory'])test('bekleyen sipariş kurtarma: '+outage,async({request})=>{
 const before=await stock(request);expect(before.available).toBeGreaterThan(0);
 const key=require('node:crypto').randomUUID();let order;
 local('stop',outage);
 try {
  order=await create(request,key);expect(order.status).toBe('Pending');
  const pending=await request.get('/api/orders/'+order.id);
  expect(pending.ok()).toBeTruthy();expect((await pending.json()).status).toBe('Pending');
  local('restart','orders');
 } finally {local('start',outage);}
 await confirmed(request,order.id);
 await ready(request);
 const replay=await create(request,key);expect(replay.id).toBe(order.id);
 local('restart','inventory','orders');
 await ready(request);
 await confirmed(request,order.id);
 await expect.poll(async()=> (await stock(request)).quantity,{timeout:60000}).toBe(before.quantity-1);
 await new Promise(resolve=>setTimeout(resolve,3000));
 expect((await stock(request)).quantity).toBe(before.quantity-1);
 expect((await stock(request)).available).toBe(before.available-1);
});
