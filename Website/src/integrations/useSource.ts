import {useEffect,useState,useCallback} from 'react';
import {SourceError} from './validation';
import type {Resource} from './resource';
export function useSource<T>(key:string|null,loader:(force:boolean)=>Promise<Resource<T>>){
 const [state,setState]=useState<{status:'unconfigured'|'loading'|'ready'|'error';result?:Resource<T>;error?:SourceError}>({status:key?'loading':'unconfigured'}),[attempt,setAttempt]=useState(0),[retryAllowed,setRetryAllowed]=useState(true);
 useEffect(()=>{let active=true;if(!key){setState({status:'unconfigured'});return;}setState({status:'loading'});loader(attempt>0).then(result=>{if(active)setState({status:'ready',result});}).catch(e=>{if(active)setState({status:'error',error:e instanceof SourceError?e:new SourceError('network')});});return()=>{active=false;};},[key,attempt]);
 const error=state.error??state.result?.warning;
 useEffect(()=>{const wait=(error?.retryAt??0)-Date.now();setRetryAllowed(wait<=0);if(wait>0){const timer=setTimeout(()=>setRetryAllowed(true),Math.min(wait,2147483647));return()=>clearTimeout(timer);}},[error]);
 const retry=useCallback(()=>setAttempt(v=>v+1),[]);
 return {...state,retry,retryAllowed};
}
