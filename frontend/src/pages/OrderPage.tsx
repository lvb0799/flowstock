import {useState} from 'react';
import OrderForm from '../components/OrderForm';
import OrderDetailModal from '../components/OrderDetailModal';
import { api } from '../api/client';
import { OrderCreatePayload, OrderDetailResponse, OrderKind, Row } from '../types';

export default function OrderPage({kind,parties,products,orders,submit,onError,onVoid}:{kind:OrderKind;parties:Row[];products:Row[];orders:Row[];submit:(payload:OrderCreatePayload)=>Promise<void>;onError?:(message:string)=>void;onVoid?:(id:number)=>Promise<void>}){
  const sale=kind==='sales'; const [detail,setDetail]=useState<OrderDetailResponse|null>(null); const [loadingId,setLoadingId]=useState<number|null>(null);
  const openDetail=async(row:Row)=>{
    const id=Number(row.Id ?? row.id);
    if(!id){onError?.('無法取得單據 ID');return;}
    try{
      setLoadingId(id);
      const result=await api(`/${kind}/${id}`);
      if(!result?.header || !Array.isArray(result?.items)) throw new Error('明細 API 回傳格式不正確');
      setDetail(result);
    }catch(e:any){
      onError?.(e?.message || `${sale?'銷貨':'進貨'}明細讀取失敗`);
    }finally{
      setLoadingId(null);
    }
  };
  return <><div className="page-title-row"><div><h1>{sale?'銷貨':'進貨'}</h1><p>一張單可建立多筆商品明細，送出後會一次更新庫存。</p></div></div>
    <OrderForm kind={kind} parties={parties} products={products} submit={submit}/>
    <div className="tablewrap"><table><thead><tr><th>單號</th><th>{sale?'客戶':'供應商'}</th><th>日期</th><th>明細筆數</th><th>總金額</th><th>{sale?'付款狀態':'狀態'}</th><th>操作</th></tr></thead><tbody>
      {orders.map(r=>{const rowId=Number(r.Id ?? r.id);return <tr key={rowId}><td>{r.OrderNo ?? r.orderNo}</td><td>{sale?(r.CustomerName ?? r.customerName):(r.SupplierName ?? r.supplierName)}</td><td>{r.OrderDate ?? r.orderDate}</td><td>{r.ItemCount ?? r.itemCount ?? 0}</td><td className="money">{Number(r.TotalAmount ?? r.totalAmount ?? 0).toLocaleString()}</td><td>{sale?(r.PaymentStatus ?? r.paymentStatus):(r.Status ?? r.status)}</td><td><div className="actions"><button className="btn-edit" disabled={loadingId===rowId} onClick={()=>openDetail(r)}>{loadingId===rowId?'讀取中':'查看明細'}</button>{String(r.Status??r.status)!=='VOID'&&<button className="btn-delete" onClick={async()=>{if(window.confirm(`確定作廢${sale?'銷貨':'進貨'}單 ${(r.OrderNo??r.orderNo)}？此操作會產生庫存反沖紀錄。`))await onVoid?.(rowId)}}>作廢</button>}</div></td></tr>})}
    </tbody></table>{orders.length===0&&<div className="empty">暫無資料</div>}</div>
    {detail&&<OrderDetailModal kind={kind} data={detail} onClose={()=>setDetail(null)}/>}</>;
}
