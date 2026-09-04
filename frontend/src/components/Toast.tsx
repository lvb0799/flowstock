export default function Toast({type,text,onClose}:{type:'success'|'error';text:string;onClose:()=>void}){
  return <div className={`toast ${type}`}>
    <div><b>{type==='success'?'操作完成':'操作失敗'}</b><span>{text}</span></div>
    <button onClick={onClose}>×</button>
  </div>;
}
