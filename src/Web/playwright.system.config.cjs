const {defineConfig}=require('@playwright/test');
module.exports=defineConfig({testDir:'./tests/system',workers:1,timeout:300000,
 reporter:[['list'],['junit',{outputFile:'../../artifacts/system.xml'}],['html',{outputFolder:'../../artifacts/system-report',open:'never'}]],
 use:{baseURL:process.env.SHOP_URL||'http://127.0.0.1:5100',channel:process.env.CI?undefined:'chrome',screenshot:'on',trace:'retain-on-failure'},
 outputDir:'../../artifacts/system-results'});
