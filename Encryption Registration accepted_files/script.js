      var usStates=new Array();
      usStates[0]=["AL","Alabama"];
      usStates[1]=["AK","Alaska"];
      usStates[2]=["AR","Arkansas"];
      usStates[3]=["AZ","Arizona"];
      usStates[4]=["CA","California"];
      usStates[5]=["CO","Colorado"];
      usStates[6]=["CT","Connecticut"];
      usStates[7]=["DC","District of Columbia"];
      usStates[8]=["DE","Delaware"];
      usStates[9]=["FL","Florida"];
      usStates[10]=["GA","Georgia"];
      usStates[11]=["GU","Guam"];
      usStates[12]=["HI","Hawaii"];
      usStates[13]=["IA","Iowa"];
      usStates[14]=["ID","Idaho"];
      usStates[15]=["IL","Illinois"];
      usStates[16]=["IN","Indiana"];
      usStates[17]=["KS","Kansas"];
      usStates[18]=["KY","Kentucky"];
      usStates[19]=["LA","Louisiana"];
      usStates[20]=["MA","Massachusetts"];
      usStates[21]=["MD","Maryland"];
      usStates[22]=["ME","Maine"];
      usStates[23]=["MI","Michigan"];
      usStates[24]=["MN","Minnesota"];
      usStates[25]=["MO","Missouri"];
      usStates[26]=["MS","Mississippi"];
      usStates[27]=["MT","Montana"];
      usStates[28]=["NC","North Carolina"];
      usStates[29]=["ND","North Dakota"];
      usStates[30]=["NE","Nebraska"];
      usStates[31]=["NH","New Hampshire"];
      usStates[32]=["NJ","New Jersey"];
      usStates[33]=["NM","New Mexico"];
      usStates[34]=["NV","Nevada"];
      usStates[35]=["NY","New York"];
      usStates[36]=["OH","Ohio"];
      usStates[37]=["OK","Oklahoma"];
      usStates[38]=["OR","Oregon"];
      usStates[39]=["PA","Pennsylvania"];
      usStates[40]=["RI","Rhode Island"];
      usStates[41]=["SC","South Carolina"];
      usStates[42]=["SD","South Dakota"];
      usStates[43]=["TN","Tennessee"];
      usStates[44]=["TX","Texas"];
      usStates[45]=["UT","Utah"];
      usStates[46]=["VA","Virginia"];
      usStates[47]=["VT","Vermont"];
      usStates[48]=["WA","Washington"];
      usStates[49]=["WI","Wisconsin"];
      usStates[50]=["WV","West Virginia"];
      usStates[51]=["WY","Wyoming"];
      usStates[52]=["PR","PUERTO RICO"];

      var where = ""; // which link
      var plusSignImg = "plusButton.gif";
      var minusSignImg = "minusButton.gif";
      var collapseFlag='Collapse All';
      var expandFlag = 'Expand All';
      
        
      function checkwhere(e) {
        if (document.layers){
          xCoord = e.x;
          yCoord = e.y;
        }
        else if (document.all){
          xCoord = event.clientX;
          yCoord = event.clientY;
        }
        else if (document.getElementById){
          xCoord = e.clientX;
          yCoord = e.clientY;
        }
        //self.status = "X= "+ xCoord + "  Y= " + yCoord;
    
      }
      function scrollT(val){
        //window.scrollTo(0,900);
        window.scrollTo(0,val);
      }

      function activateSubmit(form) {
        if(form.submitVerify.checked) {
          form.submitConfirm.disabled = false;
        } else {
          form.submitConfirm.disabled = true;
        }
      }

      function newWinPrint(url)
      { var title= "Print";
        var winRef = window.open(url,title,"scrollbars=yes,menubar=yes,resizable=no,status=yes,toolbar=yes,location=no,width=800,height=580")
        winRef.focus();
      } 
      
      function deleteWorkItem(id)
      {
        if(confirm('Are you sure you want to delete this work item?')){
          document.forms[1].action='exp/WorkItem/'+id+'?action=Delete%20Work%20Item';
          document.forms[1].submit();
        }
      }
      function reuseWorkItem(id)
      {
        if(confirm('Are you sure you want to reuse this work item?')){
          document.forms[0].action='exp/WorkItem/'+id+'/CopyLoad';
          document.forms[0].submit();
        }
      }
      
      function doSwitchAll() {
      	var elem = document.getElementById('switchDisplay');
      	
      	if(elem.childNodes[0].data != null && elem.childNodes[0].data != undefined
      	    && elem.childNodes[0].data.indexOf(expandFlag)>=0){
      	  elem.childNodes[0].data=elem.childNodes[0].data.replace(expandFlag,collapseFlag);
      		switchDisplay('domElem',expandFlag);    	
      	}
      	else if(elem.childNodes[0].data != null && elem.childNodes[0].data != undefined
      	    && elem.childNodes[0].data.indexOf(collapseFlag)>=0){
      	  elem.childNodes[0].data=elem.childNodes[0].data.replace(collapseFlag, expandFlag);
      		switchDisplay('domElem',collapseFlag);
      	}
      }
           
      function switchDisplay(obj,flag) {
        
        var elems = document.getElementsByTagName('div');
        
        for(var i=0;i<elems.length;i++) {
          var id = elems[i].id;
          if(id !=null && id != undefined && id.indexOf(obj)>=0) {
          	
            if ( id.indexOf('CollapseImg')>=0){
              switchCollapseImage(elems[i], flag);
            }
            else{
              if ( !isCollapsed(elems[i]) && flag == collapseFlag) {
                elems[i].style.display = 'none';
              }
              else if(isCollapsed(elems[i]) && flag == expandFlag) {
                elems[i].style.display = '';
              }
            }
          }
        }
      } 
      function isCollapsed(elem){
        if( elem != null && elem != undefined &&
              elem.style.display == 'none' ) {
          return true;
        }
        return false;
      }
      
      function switchCollapseImage(elem, flag){
        if(elem != null & elem != undefined && elem.childNodes.length > 0){
        
          for(var j=0; j<elem.childNodes.length; j++){
            var tagName = elem.childNodes[j].tagName;
            
            if(tagName!=null && tagName!=undefined && tagName.indexOf('IMG')>=0){
              elem.childNodes[j].src = getCollapseImagePath(elem.childNodes[j].src, flag);
            }  
          }  
          
          var prefix = elem.id.replace('CollapseImg','');   
          saveBlockState(prefix,flag);
          //alert(getBlockStateElement().value);
        }       
      }   
      
      
      function getCollapseImagePath(img, flag){
 
        if(img !=null && img != undefined 
             && img.indexOf(plusSignImg)>=0 && flag == expandFlag){
          var startAt = img.indexOf(plusSignImg);
          return img.substring(0,startAt) + minusSignImg; 
        }
        else if(img !=null && img != undefined 
             && img.indexOf(minusSignImg)>=0 && flag == collapseFlag){
          var startAt = img.indexOf(minusSignImg);
          return img.substring(0,startAt) + plusSignImg;
        }
      	else {
      		return img;
      	}
      }
      
      function switchLocalDisplay(obj) {
        
        var elems = document.getElementsByTagName('div');
        
        for(var i=0;i<elems.length;i++) {
          var id = elems[i].id;
          if(id !=null && id != undefined && id.indexOf(obj)>=0) {
          	
          	var flag = selectActionFlag(elems[i]);
                        
            if ( id.indexOf('CollapseImg')>=0){
							
              switchCollapseImage(elems[i], flag);
            }
            else{
              if ( !isCollapsed(elems[i])) {
                elems[i].style.display = 'none';
              }
              else if(isCollapsed(elems[i])) {
                elems[i].style.display = '';
              }
            }
          }
        }
      }
      
      function selectActionFlag (elem) {
      	
      	if(elem !=null && elem != undefined){

      		for(var i=0; i<elem.childNodes.length; i++) {
            var tagName = elem.childNodes[i].tagName;
          
            if(tagName!=null && tagName!=undefined && tagName.indexOf('IMG')>=0 ){
            	var img = elem.childNodes[i].src;

            	if(img !=null && img !=undefined && img.indexOf(minusSignImg)>=0){
            		//In expand mode, the next action is collapse;
            		return collapseFlag;
            	}
            	if(img !=null && img !=undefined && img.indexOf(plusSignImg)>=0){
            		//In collapse mode, the next action is expand;
            		return expandFlag;
            	}            	
            }
      		}	
      	}
      }
      
     
      function getBlockStateElement(){
      	return document.getElementById('blockState');
      }
      
      function saveBlockState(blockPrefix, flag){
      	  var rec = blockPrefix;
      	  
          if(flag == collapseFlag){
          	rec = rec + ' C,';
          }
        	else{
        		rec = rec + ' E,';
        	}
          //Record the elem prefix plus its flag in the blockState field       	
          var elem = getBlockStateElement();
          
          if(elem == null || elem == undefined)
          	return;
          	
          var elemValue = elem.value;
          
          if(elemValue.indexOf(blockPrefix)>=0){
	        	//Remove any previous setting;
	          elemValue = elemValue.replace(blockPrefix + ' C,','');
	          elemValue = elemValue.replace(blockPrefix + ' E,','');
	        }
	        
	        elemValue = elemValue + rec;
	        elem.value = elemValue;
      }
      
      function doCollapse(){
     		var blockState = document.getElementById('blockState').value;	
     		
     		if(blockState != null && blockState != undefined){
     			var stateArray = blockState.split(',');
     			for(var i=0;i<stateArray.length;i++) {
     				if(stateArray[i] != null && stateArray[i] != undefined 
     				    && stateArray[i].indexOf(' C')>=0){
     					
     					var itemValue = stateArray[i];
     					itemValue = itemValue.replace(' C','');
     					switchDisplay(itemValue,collapseFlag);
     				}
     			}
     		}      	
      	
      }
      function getfocus(idName){

				
      }
    
    function setReturnHash(returnHash) {
      document.forms[0].locationSet.value = returnHash;
    }

function havePopupBlocker() {
  var myTest = window.open("about:blank","","directories=no,height=1,width=1,menubar=no,resizable=no,scrollbars=no,status=no,titlebar=no,top=6000,location=no");
  if (!myTest) {
    return true;
  } else {
    myTest.close();

    return false;
  }
}

function showStateProvince(location, countryCode, fieldName){

	
	var elem = document.getElementById(location);
	var fieldId = location + 'Input';
	
	if(elem != null && elem != undefined){

		var newChild = null;

    if(countryCode == 'US'){
    	newChild = createStateList(fieldId,fieldName);
    }
  	else{
    	newChild = createProvinceField(fieldId,fieldName);
  	}
  			
  	for(var i=0; i<elem.childNodes.length; i++){
  		if(elem.childNodes[i].id != null && elem.childNodes[i].id !=undefined &&
  		  elem.childNodes[i].id.indexOf(fieldId)>=0){
  			elem.replaceChild(newChild,elem.childNodes[i]);
  		}
    }
	}
  else{
  	alert('Warning: Cannot find Location');
  }
}

function createStateList(id,name){
	var stSelect = document.createElement("select");
	stSelect.name=name;
	stSelect.id = id;
	
	stSelect.options[0]= new Option("Please Select          "," ");
	for (i=0; i<usStates.length; i++){
		stSelect.options[i+1]=new Option(usStates[i][1],usStates[i][0]);
	}
  stSelect.className='editText';
	return stSelect;
}

function createProvinceField(id,name){
	var provText = document.createElement("input");
	provText.type='text';
	provText.id=id;
	provText.name=name;
	provText.className='editText';
	provText.maxLength=50;
	return provText;
}

function oneExportItem(seqN) {

	var answer = confirm ("Are you sure you want to delete this export item?");
	
	if (answer) {
		var nEl = document.createElement("input");
		nEl.setAttribute("type", "hidden");
		nEl.setAttribute("name", "seq");
		nEl.setAttribute("value", seqN);
		document.forms[0].appendChild(nEl);
		document.forms[0].locationSet.value  = 'exportItem';
		return true;
	} else {
		return false;
	}
	
}

function twoExportItem(seqN) {

	
	var nEl = document.createElement("input");
	nEl.setAttribute("type", "hidden");
	nEl.setAttribute("name", "seq");
	nEl.setAttribute("value", seqN);
	document.forms[0].appendChild(nEl);
	document.forms[0].locationSet.value  = 'exportItem';
    return true;
	
}

function threeExportItem(seqN) {

	var answer = confirm ("Are you sure you want to delete this end user?");
	
	if (answer) {
		var nEl = document.createElement("input");
		nEl.setAttribute("type", "hidden");
		nEl.setAttribute("name", "seq");
		nEl.setAttribute("value", seqN);
		document.forms[0].appendChild(nEl);
		document.forms[0].locationSet.value  = 'endUser';
		return true;
	} else {
		return false;
	}
	
}

function fourExportItem(seqN) {

	
	var nEl = document.createElement("input");
	nEl.setAttribute("type", "hidden");
	nEl.setAttribute("name", "seq");
	nEl.setAttribute("value", seqN);
	document.forms[0].appendChild(nEl);
	document.forms[0].locationSet.value  = 'endUser';
    return true;
	
}

function fiveExportItem(seqN) {

	var answer = confirm ("Are you sure you want to delete this ultimate consignee?");
	
	if (answer) {
		var nEl = document.createElement("input");
		nEl.setAttribute("type", "hidden");
		nEl.setAttribute("name", "seq");
		nEl.setAttribute("value", seqN);
		document.forms[0].appendChild(nEl);
		document.forms[0].locationSet.value  = 'ultConsig';
		return true;
	} else {
		return false;
	}
	
}

function sixExportItem(seqN) {

	
	var nEl = document.createElement("input");
	nEl.setAttribute("type", "hidden");
	nEl.setAttribute("name", "seq");
	nEl.setAttribute("value", seqN);
	document.forms[0].appendChild(nEl);
	document.forms[0].locationSet.value  = 'ultConsig';
    return true;
	
}

function deleteExportItem(deleteURL) {

	var answer = confirm ("Are you sure you want to delete this export item?");
	//alert (document.forms[0].action);
	//alert (document.forms[0].method);

	if (answer) {
		//window.location.search="?action=Delete&seq=1";
		//window.location.href=deleteURL;
		document.forms[0].action = 'exp/ExportLicenseApplicationSave/144175?seq=0';
		return true;
	}
	else {
		alert (window.location.href);
		//window.history.back();
		return false;
	}
}

function hideAndCheckWiEdit(obj) {
	obj.hidden = true;
	checkWiEdit(obj.form);
}

function checkWiEdit(form) {
	//alert ("in checkWiEdit:" + form.action);
	var checkEuflag = "false";
	var checkExpflag = "false";
	var msg ;
	
	if (form.deletedEndUsers){
		if(form.deletedEndUsers.length == undefined){
		  if(form.deletedEndUsers.checked){
			  checkEuflag = "true";
			}
	  }
	  else if(form.deletedEndUsers.length >0){
			for(i=0; i<form.deletedEndUsers.length; i++){
				if(form.deletedEndUsers[i].checked){
					checkEuflag = "true";
				}
			}
		}
		else{
			checkEuflag = "false"
		}
	}
	
	if (form.deletedExportItems){
		if(form.deletedExportItems.length == undefined){
		  if(form.deletedExportItems.checked){
			  checkExpflag = "true";
			}
		}
		else if(form.deletedExportItems.length >0){
			for(i=0; i<form.deletedExportItems.length; i++){
				if(form.deletedExportItems[i].checked){
					checkExpflag = "true";
				}
			}
		}
		else{
			checkExpflag = "false"
		}		
	}
		
	if(checkEuflag == "true"){
		msg = 'one or more End User(s)';
	}
	if(checkExpflag == "true"){
		msg = 'one or more  Export Item(s)';
	}
	if(checkEuflag=="true" && checkExpflag=="true"){
		msg = 'one or more End User(s) and Export Item(s)';
	}
	if(msg){
		if(confirm('You have selected '+ msg + ' for deletion. Are you sure you want to delete these items?')){
			return true;
		}
		else{
			return false;
		}
	}
	else{
		return true;;
	}

}

function editPop(linkObject)
{
	  
      var parentObject = linkObject.parentNode;
      
       //traverse up DOM from the linkObject to the <tr> node
      while (parentObject !== null && parentObject.tagName.toLowerCase() != 'table') 
      {
    	  parentObject = parentObject.parentNode;
      }
      
      if (parentObject === null) 
    	  return;
      
      //we are only concerned with table rows 3, 5 and 7
      // (index 2,4,6)
      
      firstRow = parentObject.rows[2];
      secondRow = parentObject.rows[4];
      thirdRow = parentObject.rows[6];
      
      
      overlay = document.createElement('div');
      overlay.style.position='fixed';
      overlay.id='overlay';
      overlay.style.width='100%';
      overlay.style.height='100%';
      overlay.style.backgroundImage='url(/docs/images/trans.png)';      
      overlay.style.backgroundRepeat='repeat';
      overlay.style.top='0px';
      overlay.style.left='0px';
      overlay.innerHTML=
    	  '<div style="margin-left:auto;margin-right:auto; width:300px; height:200px; margin-top:20%; border:1px solid #000000; background-color:#ffffff;">' + 
    	  '  <div style="padding:3px; font-weight:bold; color:#ffffff; background-color:#383a7f;">' +    
    	  '    Edit Export Item' + 
    	  '  </div><div id="popup" style="font-size:0.9em;">' +
    	  '&nbsp;ECCN:&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;' + 
    	  '</div></div>';
      
      document.getElementsByTagName('form')[0].appendChild(overlay);
      
      eccnNode = document.getElementById('exportItemEccn').cloneNode(true);
      eccnNode.value = trim(firstRow.cells[1].firstChild.data);
      document.getElementById('popup').appendChild(eccnNode);
      
      buttonDiv = document.createElement('div');
      buttonDiv.style.margin='110px 10px 0px 0px';
      buttonDiv.style.textAlign='right';
      buttonDiv.innerHTML = '<input value="Cancel" type="button" onclick="document.getElementsByTagName(\'body\')[0].removeChild(document.getElementById(\'overlay\'))"/>';
      document.getElementById('popup').appendChild(buttonDiv);
      
}

function trim(str, chars) {
    return ltrim(rtrim(str, chars), chars);
}

function ltrim(str, chars) {
    chars = chars || "\\s";
    return str.replace(new RegExp("^[" + chars + "]+", "g"), "");
}

function rtrim(str, chars) {
    chars = chars || "\\s";
    return str.replace(new RegExp("[" + chars + "]+$", "g"), "");
}

function switchControlDisplay(obj, elemId1, elemId2){

	if (obj.checked) {
		document.getElementById(elemId1).style.display='none';
		document.getElementById(elemId2).style.display='';
		document.getElementById(elemId1).value = document.getElementById(elemId2).value;
	} else {
	  document.getElementById(elemId1).style.display='';
	  document.getElementById(elemId1).value='';
	  document.getElementById(elemId2).style.display='none';
	}
}

var urlSession;
var reqSession;
function verifyAddress(obj, id, seq, st1, st2, ct, cc, zip, ow) {
    obj.hidden = true;

    var flag;
    if(ow == null){
    	flag = 'false';
    }else{
    	flag = ow.checked;
    }
	urlSession = contextPathSession + "/exp/WiUpdate?id=" + id +"&seq=" + seq + "&st1=" + escape(st1.value) + "&st2=" + escape(st2.value) + "&ct=" + escape(ct.value) + "&cc=" + cc.value + "&zip=" + escape(zip.value) + "&ow=" + flag;
    
	window.location = urlSession;
}

function toggleConfirm(cbObj, tgrObj) {
	obj = document.getElementById(tgrObj);
	if (cbObj.checked){
		obj.disabled = false;
	}else{
		obj.disabled = true;
	}
}

function toggleButton(obj) {
	obj.disabled = false;
}