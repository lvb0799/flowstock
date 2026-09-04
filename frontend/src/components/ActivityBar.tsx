import { ActivityHistory } from '../types';
import { actionLabel } from '../utils/format';

export default function ActivityBar({latest,onOpenHistory}:{latest?:ActivityHistory;onOpenHistory:()=>void}){
 const text=latest ? `${actionLabel[latest.Action]||latest.Action} 1 筆資料｜${latest.EntityType}${latest.EntityLabel?`：${latest.EntityLabel}`:''}` : '尚無異動';
 return <div className="activity-bar" onClick={onOpenHistory} title="點擊查看異動紀錄"><span className="activity-dot"></span><b>最近異動：</b><span>{text}</span><button>查看 History</button></div>;
}
