import React,{useMemo,useState} from 'react';
import { OrderCreatePayload, OrderItemInput, OrderKind, Row } from '../types';

type Props={
  kind:OrderKind;
  parties:Row[];
  products:Row[];
  submit:(payload:OrderCreatePayload)=>Promise<void>;
};

const emptyItem=():OrderItemInput=>({productId:0,qty:1,unitPrice:0});

export default function OrderForm({kind,parties,products,submit}:Props){
  const isSale=kind==='sales';
  const [partyId,setPartyId]=useState(0);
  const [orderDate,setOrderDate]=useState(new Date().toISOString().slice(0,10));
  const [note,setNote]=useState('');
  const [items,setItems]=useState<OrderItemInput[]>([emptyItem()]);
  const [error,setError]=useState('');
  const [saving,setSaving]=useState(false);

  const total=useMemo(()=>items.reduce((sum,x)=>sum+(Number(x.qty)||0)*(Number(x.unitPrice)||0),0),[items]);

  const patchItem=(index:number,patch:Partial<OrderItemInput>)=>{
    setError('');
    setItems(prev=>prev.map((x,i)=>i===index?{...x,...patch}:x));
  };

  const selectProduct=(index:number,productId:number)=>{
    if(productId>0 && items.some((x,i)=>i!==index&&x.productId===productId)){
      setError('同一張單不可重複加入相同商品');
      return;
    }
    const p=products.find(x=>Number(x.Id)===productId);
    patchItem(index,{productId,unitPrice:p?Number(isSale?p.SalePrice:p.CostPrice)||0:0});
  };

  const addItem=()=>setItems(prev=>[...prev,emptyItem()]);
  const removeItem=(index:number)=>setItems(prev=>prev.length===1?[emptyItem()]:prev.filter((_,i)=>i!==index));

  const onSubmit=async(e:React.FormEvent)=>{
    e.preventDefault();
    setError('');
    if(!partyId){setError(isSale?'請選擇客戶':'請選擇供應商');return;}
    if(items.some(x=>!x.productId)){setError('每筆明細都必須選擇商品');return;}
    if(items.some(x=>Number(x.qty)<=0)){setError('數量必須大於 0');return;}
    if(items.some(x=>Number(x.unitPrice)<0)){setError('單價不可小於 0');return;}
    const ids=items.map(x=>x.productId);
    if(new Set(ids).size!==ids.length){setError('同一張單不可重複加入相同商品');return;}
    if(isSale){
      const shortage=items.find(x=>{
        const p=products.find(p=>Number(p.Id)===x.productId);
        return p && Number(x.qty)>Number(p.StockQty);
      });
      if(shortage){
        const p=products.find(p=>Number(p.Id)===shortage.productId);
        setError(`商品「${p?.Name??shortage.productId}」庫存不足，目前庫存 ${p?.StockQty??0}`);
        return;
      }
    }
    const payload:OrderCreatePayload={
      [isSale?'customerId':'supplierId']:partyId,
      orderDate,
      note,
      items:items.map(x=>({productId:Number(x.productId),qty:Number(x.qty),unitPrice:Number(x.unitPrice)}))
    };
    try{
      setSaving(true);
      await submit(payload);
      setPartyId(0); setNote(''); setItems([emptyItem()]); setOrderDate(new Date().toISOString().slice(0,10));
    }catch(e:any){setError(e?.message||'建立單據失敗');}
    finally{setSaving(false);}
  };

  return <form onSubmit={onSubmit} className="order-card">
    <div className="order-header-fields">
      <label><span>{isSale?'客戶':'供應商'}</span><select value={partyId} onChange={e=>setPartyId(Number(e.target.value))} required><option value={0}>{isSale?'選客戶':'選供應商'}</option>{parties.map(x=><option key={x.Id} value={x.Id}>{x.Code} {x.Name}</option>)}</select></label>
      <label><span>單據日期</span><input type="date" value={orderDate} onChange={e=>setOrderDate(e.target.value)}/></label>
      <label className="order-note"><span>備註</span><input value={note} onChange={e=>setNote(e.target.value)} placeholder="備註（選填）"/></label>
    </div>

    <div className="order-lines-title"><div><b>商品明細</b><small>可加入多筆商品</small></div><button type="button" className="btn-add-line" onClick={addItem}>＋ 新增商品</button></div>
    <div className="order-lines-wrap"><table className="order-lines"><thead><tr><th>#</th><th>商品</th><th>目前庫存</th><th>數量</th><th>單價</th><th>小計</th><th></th></tr></thead><tbody>
      {items.map((item,index)=>{
        const p=products.find(x=>Number(x.Id)===item.productId);
        const amount=(Number(item.qty)||0)*(Number(item.unitPrice)||0);
        return <tr key={index}>
          <td>{index+1}</td>
          <td><select value={item.productId} onChange={e=>selectProduct(index,Number(e.target.value))}><option value={0}>選商品</option>{products.map(x=><option key={x.Id} value={x.Id}>{x.Sku}｜{x.Name}</option>)}</select></td>
          <td>{p?`${p.StockQty} ${p.Unit??''}`:'-'}</td>
          <td><input type="number" min="0.01" step="0.01" value={item.qty} onChange={e=>patchItem(index,{qty:Number(e.target.value)})}/></td>
          <td><input type="number" min="0" step="0.01" value={item.unitPrice} onChange={e=>patchItem(index,{unitPrice:Number(e.target.value)})}/></td>
          <td className="money">{amount.toLocaleString()}</td>
          <td><button type="button" className="btn-remove-line" onClick={()=>removeItem(index)}>刪除</button></td>
        </tr>})}
    </tbody></table></div>
    {error&&<div className="form-error">{error}</div>}
    <div className="order-summary"><span>共 {items.length} 筆明細</span><strong>總金額：NT$ {total.toLocaleString()}</strong><button disabled={saving}>{saving?'建立中...':`建立${isSale?'銷貨':'進貨'}單`}</button></div>
  </form>;
}
