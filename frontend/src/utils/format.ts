export const money = (value: any) =>
  Number(value || 0).toLocaleString('zh-TW', { maximumFractionDigits: 2 });

export const labelMap: Record<string, string> = {
  products: '商品', customers: '客戶', suppliers: '供應商',
  purchases: '進貨單', sales: '銷貨單'
};

export const actionLabel: Record<string, string> = {
  CREATE: '新增', UPDATE: '修改', DELETE: '刪除', PAYMENT: '收款', ISSUE: '開立', RETURN: '退貨', REFUND: '退款', INVOICE_ADJUST: '發票調整', VOID: '作廢', COUNT: '盤點'
};
