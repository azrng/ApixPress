-- 接口树扁平化：历史数据把未分组接口归入"默认模块"虚拟目录，统一提升到根目录，
-- 与新建目录（entry_type='folder'）承载的两级目录结构保持一致；重复执行无副作用
UPDATE request_cases SET folder_path = '' WHERE folder_path = '默认模块';
