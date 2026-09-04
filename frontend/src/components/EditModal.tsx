import React from 'react';
import { EditState } from '../types';
import { labelMap } from '../utils/format';

export default function EditModal({edit,onCancel,onSave}:{edit:Exclude<EditState,null>;onCancel:()=>void;onSave:(body:Record<string,any>)=>void}){
 const isProduct=edit.kind==='products'; const r=edit.row;
 const submit=(e:React.FormEvent<HTMLFormElement>)=>{e.preventDefault();onSave(Object.fromEntries(new FormData(e.currentTarget)))};
 return <div className="modal-mask"><div className="modal"><div className="modal-head"><h2>修改{labelMap[edit.kind]}</h2><button onClick={onCancel}>×</button></div><form onSubmit={submit} className="modal-form">
 {isProduct?<><label>SKU<input name="Sku" defaultValue={r.Sku} required/></label><label>商品名稱<input name="Name" defaultValue={r.Name} required/></label><label>單位<input name="Unit" defaultValue={r.Unit}/></label><label>售價<input name="SalePrice" type="number" step="0.01" defaultValue={r.SalePrice}/></label><label>成本<input name="CostPrice" type="number" step="0.01" defaultValue={r.CostPrice}/></label></>:<><label>代碼<input name="Code" defaultValue={r.Code} required/></label><label>名稱<input name="Name" defaultValue={r.Name} required/></label><label>電話<input name="Phone" defaultValue={r.Phone||''}/></label><label>Email<input name="Email" defaultValue={r.Email||''}/></label><label>地址<input name="Address" defaultValue={r.Address||''}/></label></>}
 <div className="modal-actions"><button type="button" className="btn-secondary" onClick={onCancel}>取消</button><button type="submit">儲存修改</button></div></form></div></div>;
}
