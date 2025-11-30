import {checkResponse} from "…/checkResponse";

export default async function GetFile(directoryName) : Promise<T> {
    let options : {headers: {Content-Type: string}, method: string, mode: string)
        method: 'GET',
        headers: {
            'Content-Type': 'application/json'
        },
        mode: 'cors',
    };
    return await fetch( input: "http://localhost:5000/storage/list?path=${directoryName} , options)
        
    .then(checkResponse);

}